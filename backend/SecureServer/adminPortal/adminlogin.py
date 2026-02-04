import sys, pyotp, uuid, urllib.parse, os, time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent.parent))
from SecureServer.code.encryption import hash_pw, verify_pw
from SecureServer.code.file_handling import load_users, save_users, load_failed_attempts, save_failed_attempts
from SecureServer.code.token_handling import get_new_token, validate_token
from SecureServer.code.logs import server_log

# Configuration
APP_NAME = "SecureServerAdmin"
MAX_LOGIN_FAILURES = 5
LOCKOUT_LOGIN_WINDOW = 15 * 60  # 15 minutes

def authenticate_session(session: str):
    user, t_data = validate_token(session)
    if not t_data or not user:
        # Session expired or nonexistant
        server_log("SECURITY NOTICE", f"Failed session fetch for admin user session {session}.")
        return None
    
    return user # No extra logs, as other files will handle that


def get_totp_uri(username: str, secret: str) -> str:
    # Label = issuer:username
    label = f"{APP_NAME}:{username}"
    label = urllib.parse.quote(label)  # URL-encode special characters
    
    issuer = urllib.parse.quote(APP_NAME)
    
    # Standard URI format
    uri = f"otpauth://totp/{label}?secret={secret}&issuer={issuer}&algorithm=SHA1&digits=6&period=30"
    return uri

def create_initial_admin(username: str, password: str):
    users = load_users()

    if len(users) > 0:
        return False  # Initial admin already exists

    password_hash = hash_pw(password)

    new_user = {
        "id": str(uuid.uuid4()),
        "username": username,
        "password": password_hash,
        "root_auth": True,
        "dev_admin": True,
        "root": True, # This flags the initial account on the server, as it will not follow a setup template
        "salt": os.urandom(16).hex(),
        "2fa_secret": pyotp.random_base32(),
        "2fa_enabled": True,
        "2fa_setup_complete": False
    }

    users.append(new_user)
    save_users(users)

    server_log("NOTICE", f"Initial Developer Admin '{username}' created.")

    # Give 2fa authentication setup
    totp_uri = get_totp_uri(username, new_user["2fa_secret"])
    server_log("NOTICE", f"Served initial 2FA activation code for developer admin user {username}.")
    return 5, totp_uri



def authenticate(username: str, password: str, totp_code: str | None = None):
    """
    Returns
    | Code | Meaning                   |
    | ---- | ------------------------- |
    | `0`  | Root authenticated        |
    | `1`  | Non-root authenticated    |
    | `3`  | 2FA required (OTP prompt) |
    | `5`  | 2FA setup required (QR)   |
    | `4`  | Invalid OTP               |
    | `2`  | Failure                   |
    | `6`  | Account locked            |
    | `7`  | Account frozen            |
    """
    users = load_users()
    failed_attempts = load_failed_attempts()
    now = time.time()

    # FIRST RUN: only if no users exist 
    if len(users) == 0:
        return create_initial_admin(username, password)

    user = next((u for u in users if u["username"] == username), None)

    # Prepare dummy hash for timing-attack protection
    target_hash = user["password"] if user else hash_pw("dummy")

    # --- Check lockout ---
    attempts = failed_attempts.get(username, [])
    # Keep only recent attempts within lockout window
    attempts = [ts for ts in attempts if now - ts < LOCKOUT_LOGIN_WINDOW]
    failed_attempts[username] = attempts

    if len(attempts) >= MAX_LOGIN_FAILURES:
        remaining = int(LOCKOUT_LOGIN_WINDOW - (now - min(attempts)))
        server_log("SECURITY NOTICE", f"Account locked for user {username} due to repeated failures.")
        save_failed_attempts(failed_attempts)
        return 6, f"Account temporarily locked. Try again in {remaining // 60} minutes."

    if not user or not verify_pw(password, target_hash) or not user.get("dev_admin", False):
        # Failed login → record attempt
        attempts.append(now)
        failed_attempts[username] = attempts
        save_failed_attempts(failed_attempts)
        server_log("SECURITY NOTICE", f"Failed login for admin user {username}.")
        return 2, None

    # --- Check freeze ---
    if user.get("freeze", False):
        server_log("SECURITY NOTICE", f"Frozen admin user tried to log in: {username}")
        return 7, "Your account is disabled."

    # --- 2FA Handling ---
    if user.get("root_auth", False) or user.get("2fa_enabled", False):
        if not user.get("2fa_secret"):
            user["2fa_secret"] = pyotp.random_base32()
            user["2fa_setup_complete"] = False
            save_users(users)

        totp = pyotp.TOTP(user["2fa_secret"])

        # --- SETUP PHASE ---
        if not user.get("2fa_setup_complete", False):
            if not totp_code:
                totp_uri = get_totp_uri(username, user["2fa_secret"])
                server_log("NOTICE", f"Served initial 2FA activation code for developer admin user {username}.")
                return 5, totp_uri

            if not totp.verify(str(totp_code)):
                server_log("SECURITY NOTICE", f"Failed 2FA authentication for developer admin user {username}.")
                return 4, None

            # OTP valid → complete setup
            user["2fa_setup_complete"] = True
            save_users(users)
            server_log("LOGIN", f"Developer Admin user {username} authenticated.")
            token, key, csrf = get_new_token(user["id"], password, 1200)
            return (0 if user.get("root_auth", False) else 1), token

        # --- NORMAL 2FA ---
        if not totp_code:
            server_log("NOTICE", f"Prompted Developer Admin user {username} for 2fa.")
            return 3, None

        if not totp.verify(str(totp_code)):
            server_log("SECURITY NOTICE", f"Failed 2FA authentication for developer admin user {username}.")
            return 4, None

    # --- Successful login ---
    if username in failed_attempts:
        del failed_attempts[username]
        save_failed_attempts(failed_attempts)

    server_log("LOGIN", f"Developer Admin user {username} authenticated.")
    token, key, csrf = get_new_token(user["id"], password, 1200)
    return (0 if user.get("root_auth", False) else 1), token

if __name__ == "__main__":
    if len(sys.argv) not in (3, 4):
        server_log("ERROR", "Invalid arguments. Usage: adminlogin.py <username> <password> <(optional) TOTP>")
        sys.exit(2)
    
    username = sys.argv[1]
    password = sys.argv[2]
    totp_code = sys.argv[3] if len(sys.argv) == 4 else None

    users = load_users()

    if not username or not password:
        server_log("ERROR", "Username and password cannot be empty (adminlogin.py)")
        sys.exit(2)
    
    result, data = authenticate(username, password, totp_code)

    if (result == 5 or result == 0 or result == 1) and data:
        print(data)  # C# reads this
    sys.exit(result)