import time, uuid, os, hashlib, base64
from fastapi import Request
from typing import Optional
from cryptography.exceptions import InvalidTag

from SecureServer.code.file_handling import load_tokens, load_users, save_tokens
from SecureServer.code.encryption import hash_token, derive_vault_key, decrypt_vault, encrypt_vault
from SecureServer.code.session_store import create_session, get_session

def clean_tokens(user_id: Optional[str]) -> list:
    tokens = load_tokens() or []
    now = int(time.time())
    return [t for t in tokens if t["exp"] > now and t["user_id"] != user_id]

def get_new_token(user_id: str, password: str, expires_in: int = 3600):
    csrf = os.urandom(32).hex()
    session_id = str(uuid.uuid4())

    user_record = get_user(user_id)
    if not user_record:
        raise ValueError("User not found")

    login_secret = hashlib.pbkdf2_hmac(
        'sha256',
        password.encode(),
        bytes.fromhex(user_record["salt"]),
        600_000,
        dklen=32
    )

    create_session(session_id, login_secret)

    tokens = clean_tokens(user_id)
    now = int(time.time())

    token_plain = str(uuid.uuid4())
    token_hashed = hash_token(token_plain)

    kek = derive_vault_key(
        password=login_secret,  # pass raw bytes
        salt_hex=user_record["salt"],
        session_id=session_id
    )

    # Encrypt vault key (for cookie)
    key = encrypt_vault(b"AUTHORIZED", kek)

    tokens.append({
        "id": token_hashed,
        "user_id": user_id,
        "exp": now + expires_in,
        "auth_time": now,
        "session_id": session_id,
        "csrf": csrf,
        "safe_log": truncate_log(token_plain),
    })

    save_tokens(tokens)
    return token_plain, key, csrf

def validate_token(token: str):
    """Validate token and clean up expired tokens."""
    tokens = load_tokens()
    now = int(time.time())
    # Remove expired tokens
    tokens = [t for t in tokens if t["exp"] > now]
        
    # Find token
    token_hashed = hash_token(token)
    token_entry = next((t for t in tokens if t["id"] == token_hashed), None)
        
    save_tokens(tokens)  # save cleanup immediately
    if not token_entry:
        return None, None
    return get_user(token_entry["user_id"]), token_entry

def get_user(user_id: str):
    """Returns a user from a user id"""
    users = load_users()
    user = next((u for u in users if u["id"] == user_id), None)
    return user
def truncate_log(token: str) -> str:
    """Return last 4 characters for logging."""
    return f"***{token[-4:]}"

# --- Removes a token from user id ---
def remove_all_tokens(user_id: str):
    tokens = load_tokens()
    tokens = [t for t in tokens if t["user_id"] != user_id]
    save_tokens(tokens)


# --- Require Functions ---
def require_token(request: Request):
    token_value = request.cookies.get("auth_token")
    if not token_value:
        return {"success": False, "message": "Unauthorized - no token cookie."}

    user, t_data = validate_token(token_value)
    if not user:
        return {"success": False, "message": "Unauthorized token."}

    auth_value = request.cookies.get("auth_key")
    if not auth_value:
        return {"success": False, "message": "Missing auth key."}

    session = get_session(t_data["session_id"])
    if not session:
        return {"success": False, "message": "Session expired"}

    login_secret = session["login_secret"]

    kek = derive_vault_key(
        password=login_secret,
        salt_hex=user["salt"],
        session_id=t_data["session_id"]
    )

    try:
        key_value = decrypt_vault(auth_value, kek)
    except InvalidTag:
        return {"success": False, "message": "Invalid authentication key (decryption failed)."}
    except Exception as e:
        return {"success": False, "message": f"Invalid authentication key (unexpected error: {str(e)})"}

    return {
        "success": True,
        "user": user,
        "token": t_data,
        "key": key_value
    }


# --- CSRF verification ---
def verify_csrf(request: Request, token: dict):
    header_token = request.headers.get("X-CSRF-Token")
    if not header_token:
        return {"success": False, "message": "Missing CSRF token."}

    if header_token != token.get("csrf"):
        return {"success": False, "message": "Invalid CSRF token."}

    return {"success": True}