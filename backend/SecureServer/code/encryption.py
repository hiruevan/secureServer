import hmac, hashlib, json, base64, os, pyotp
from pathlib import Path
from base64 import urlsafe_b64decode
from cryptography.hazmat.primitives.ciphers.aead import AESGCM
from cryptography.hazmat.primitives.kdf.hkdf import HKDF
from cryptography.hazmat.primitives import hashes

from SecureServer.code.environment_variables import SYSTEM_KEY, INTEGRITY_KEY, ENCAPSILATION_KEY, REPLACE_CORRUPTED_FILES
from SecureServer.code.logs import server_log

# --- JSON logic ---
def calculate_hmac(data: str) -> str:
    """HMAC-SHA256 of string data."""
    return hmac.new(INTEGRITY_KEY.encode(), data.encode(), hashlib.sha256).hexdigest()

def load_json(file):
    if file.exists():
        if file.stat().st_size == 0:
            return []
        with open(file, "r", encoding="utf-8") as f:
            try:
                return json.load(f)
            except json.JSONDecodeError:
                return []
    return []

def write_json(file, data):
    with open(file, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2)

def load_encrypted_json(file: str, is_dict: bool = False) -> dict:
    """Load encrypted JSON dict. Auto-reset if corrupted."""
    path = Path(file)
    empty_container = {"data": {}, "signature": calculate_hmac("{}")} if is_dict else {"data": [], "signature": calculate_hmac("[]")} 

    if not path.exists():
        if REPLACE_CORRUPTED_FILES:
            server_log("WARNING", f"{file} missing — creating fresh encrypted file.")
            write_encrypted_json(file, empty_container)
        return empty_container

    try:
        enc_data = path.read_text().strip()
        decrypted = decrypt_vault(enc_data, SYSTEM_KEY)
        container = json.loads(decrypted)

        # Ensure correct keys
        if "data" not in container or "signature" not in container:
            raise ValueError("Missing required keys in container")
        return container

    except Exception as e:
        server_log("CORRUPTED ENCRYPTED FILE", f"{file} ({e})")
        if REPLACE_CORRUPTED_FILES:
            server_log("RESETTING ENCRYPTED FILE", f"{file}")
            write_encrypted_json(file, empty_container)
            return empty_container
        return {}

def write_encrypted_json(file: str, data: dict):
    """Write JSON dict as encrypted vault."""
    Path(file).write_text(encrypt_vault(json.dumps(data), SYSTEM_KEY))

# --- Aes key ---
def get_aes_key(key: str | bytes) -> bytes:
    if isinstance(key, str):
        return urlsafe_b64decode(key)
    return key 

# --- Vault encryption/decryption ---
def derive_vault_key(password: str | bytes, salt_hex: str, session_id: str = "default-id") -> bytes:
    # ensure password is bytes
    if isinstance(password, str):
        password_bytes = password.encode()
    else:
        password_bytes = password

    salt_bytes = bytes.fromhex(salt_hex)

    base_key = hashlib.pbkdf2_hmac(
        'sha256',
        password_bytes,
        salt_bytes,
        600_000,
        dklen=32
    )

    hkdf = HKDF(
        algorithm=hashes.SHA256(),
        length=32,
        salt=None,
        info=session_id.encode()
    )

    return hkdf.derive(base_key)

def generate_vault_master_key() -> str:
    """
    Generates a random 256-bit master key for vault encryption.
    This key is session-independent and stored encrypted.
    """
    master_key_bytes = os.urandom(32)
    return base64.urlsafe_b64encode(master_key_bytes).decode()

def wrap_vault_key(master_key_str: str, kek_str: str) -> str:
    """
    Wraps a master key (string) using a key encryption key (string),
    returns base64-encoded ciphertext including nonce.
    """
    master_key_bytes = master_key_str.encode('utf-8')
    kek_bytes = _derive_key(kek_str)

    aes = AESGCM(kek_bytes)
    nonce = os.urandom(12)
    ciphertext = aes.encrypt(nonce, master_key_bytes, None)
    return base64.urlsafe_b64encode(nonce + ciphertext).decode('utf-8')

def unwrap_vault_key(wrapped_str: str, kek_str: str) -> str:
    """
    Unwraps a previously wrapped master key.
    Returns the original master key as a string.
    """
    wrapped_bytes = base64.urlsafe_b64decode(wrapped_str.encode('utf-8'))
    nonce, ciphertext = wrapped_bytes[:12], wrapped_bytes[12:]
    kek_bytes = _derive_key(kek_str)

    aes = AESGCM(kek_bytes)
    master_key_bytes = aes.decrypt(nonce, ciphertext, None)
    return master_key_bytes.decode('utf-8')


def _derive_key(key_str: str) -> bytes:
        """Derive a 256-bit AES key from a string using SHA256."""
        return hashlib.sha256(key_str.encode('utf-8')).digest()  # 32 bytes

def encrypt_vault(data: str | bytes, key: str | bytes) -> str:
    key_bytes = get_aes_key(key)
    aes = AESGCM(key_bytes)
    nonce = os.urandom(12)

    # ensure data is bytes
    if isinstance(data, str):
        data_bytes = data.encode()
    else:
        data_bytes = data

    ciphertext = aes.encrypt(nonce, data_bytes, None)
    return base64.urlsafe_b64encode(nonce + ciphertext).decode()

def decrypt_vault(enc_data: str, key: str) -> str:
    if not enc_data:
        return ""
    key_bytes = get_aes_key(key)
    aes = AESGCM(key_bytes)
    raw = urlsafe_b64decode_padded(enc_data)
    nonce, ciphertext = raw[:12], raw[12:]
    return aes.decrypt(nonce, ciphertext, None).decode()


# --- Hashing ---
def hash_pw(password: str) -> str:
    salt = os.urandom(16)
    hash_bytes = hashlib.pbkdf2_hmac('sha256', password.encode(), salt, 600_000)
    return base64.b64encode(salt + hash_bytes).decode()

def verify_pw(password: str, stored: str) -> bool:
    decoded = base64.b64decode(stored.encode())
    salt = decoded[:16]
    stored_hash = decoded[16:]
    test_hash = hashlib.pbkdf2_hmac('sha256', password.encode(), salt, 600_000)
    return hmac.compare_digest(test_hash, stored_hash)

# --- Simple hashing ---
def basic_hash(string: str) -> str:
    """Hashes a string using SHA3-256 (fast, strong, no salt)."""
    return hashlib.sha3_256(string.encode()).hexdigest()

# --- Token hashing ---
def hash_token(token: str) -> str:
    """Securely hash token using HMAC-SHA256."""
    return hmac.new(ENCAPSILATION_KEY.encode(), token.encode(), hashlib.sha256).hexdigest()

# --- Safe base64 decoding helper ---
def urlsafe_b64decode_padded(data: str) -> bytes:
    """Decode a base64 string safely, adding padding if needed."""
    data = data.encode() if isinstance(data, str) else data
    padding = 4 - (len(data) % 4)
    if padding != 4:
        data += b"=" * padding
    return base64.urlsafe_b64decode(data)

# --- random base32 ---
def random_base32() -> str:
    return pyotp.random_base32()