import sys, json
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent.parent.parent))
from SecureServer.code.file_handling import load_users, load_failed_attempts
from SecureServer.code.logs import server_log
from SecureServer.adminPortal.adminlogin import authenticate_session

def sanitize_safe_users(raw_users):
    """Return safe to log fields (not passwords, secrets, entire vaults, etc)"""
    sanitized = []

    attempts = load_failed_attempts()

    for u in raw_users:
        atts = attempts.get(u.get("username"))
        if atts:
            attempts_num = len(atts)
        else:
            attempts_num = 0
        sanitized.append({
            "id": u.get("id"),
            "username": u.get("username"),
            "first_name": u.get("first_name"),
            "last_name": u.get("last_name"),
            "email": u.get("email"),
            "phone": u.get("phone"),
            "preferred_contact_method": u.get("preferred_contact_method"),
            "admin": u.get("admin", False),
            "dev_admin": u.get("dev_admin", False),
            "2fa_enabled": u.get("2fa_enabled", False),
            "root_auth": u.get("root_auth", False),
            "vault_len": len(u.get("vault", "")),
            "frozen": u.get("freeze", False),
            "failed_attempts": attempts_num
        })

    return sanitized

if __name__ == "__main__":
    # ---- Args ----
    if len(sys.argv) != 2:
        print("Invalid arguments", file=sys.stderr)
        sys.exit(2)

    session_id = sys.argv[1]

    # ---- Auth ----
    user = authenticate_session(session_id)
    if not user:
        print("Invalid session", file=sys.stderr)
        sys.exit(1)

    # ---- Serve users ----
    server_log("COMMAND", f"{user['username']} requested user list.")

    users = load_users()
    safe_users = sanitize_safe_users(users)

    print(json.dumps(safe_users))
    sys.exit(0)