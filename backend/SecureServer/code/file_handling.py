from pathlib import Path
import json, hmac, base64, os, threading
from cryptography.hazmat.primitives.ciphers.aead import AESGCM

from SecureServer.code.encryption import calculate_hmac, load_encrypted_json, write_encrypted_json
from SecureServer.code.environment_variables import REPLACE_CORRUPTED_FILES, TOKEN_KEY
from SecureServer.code.paths import USERS_FILE, TOKENS_FILE, FAILED_LOGINS_FILE
from SecureServer.code.logs import server_log

def load_users():
    """Load users with integrity check."""
    container = load_encrypted_json(USERS_FILE)

    # Verify HMAC
    data_str = json.dumps(container.get("data", []), indent=2, sort_keys=True)
    if not hmac.compare_digest(container.get("signature", ""), calculate_hmac(data_str)):
        server_log("CRITICAL", "Users file integrity check failed!")
        if REPLACE_CORRUPTED_FILES:
            server_log("RESETTING", "Users file due to integrity error")
            fresh = {"data": [], "signature": calculate_hmac(json.dumps([]))}
            write_encrypted_json(USERS_FILE, fresh)
            return []
        else:
            raise ValueError("Data integrity violation detected")

    return container["data"]
def save_users(users):
    """Save users with HMAC and encryption."""
    payload = {
        "data": users,
        "signature": calculate_hmac(json.dumps(users, indent=2, sort_keys=True))
    }
    write_encrypted_json(USERS_FILE, payload)

def load_tokens():
    """Load and decrypt the tokens dictionary from file."""
    if not os.path.exists(TOKENS_FILE):
        save_tokens({})
        return {}

    try:
        aesgcm = AESGCM(base64.urlsafe_b64decode(TOKEN_KEY))  # decode to bytes
        with open(TOKENS_FILE, "rb") as f:
            data = f.read()
            nonce, ciphertext = data[:12], data[12:]
            decrypted = aesgcm.decrypt(nonce, ciphertext, None)
            return json.loads(decrypted.decode())
    except Exception as e:
        server_log("CORRUPTED ENCRYPTED FILE", 
            f"{Path(TOKENS_FILE).name}: {type(e).__name__}")
        if REPLACE_CORRUPTED_FILES:
            server_log("RESETTING ENCRYPTED FILE", 
                f"{Path(TOKENS_FILE).name}: {type(e).__name__}")
            save_tokens({})
        return {}
def save_tokens(tokens):
    """Encrypt and save the tokens dictionary to file."""
    try:
        aesgcm = AESGCM(base64.urlsafe_b64decode(TOKEN_KEY))  # decode to bytes
        nonce = os.urandom(12)
        encrypted = aesgcm.encrypt(nonce, json.dumps(tokens).encode(), None)
        with open(TOKENS_FILE, "wb") as f:
            f.write(nonce + encrypted)
    except Exception as e:
        server_log("ERROR", f"Failed to save tokens: {type(e).__name__}")
        if REPLACE_CORRUPTED_FILES:
            print(f"RESETTING encrypted file: {TOKENS_FILE}")
            with open(TOKENS_FILE, "wb") as f:
                empty_tokens = {}
                nonce = os.urandom(12)
                encrypted = aesgcm.encrypt(nonce, json.dumps(empty_tokens).encode(), None)
                f.write(nonce + encrypted)


def load_failed_attempts():
    """Load failed attempts with encryption."""
    container = load_encrypted_json(FAILED_LOGINS_FILE, True)
    return container.get("data", {})

def save_failed_attempts(attempts):
    """Save failed attempts with encryption."""
    payload = {
        "data": attempts,
        "signature": calculate_hmac(json.dumps(attempts, indent=2, sort_keys=True))
    }
    write_encrypted_json(FAILED_LOGINS_FILE, payload)