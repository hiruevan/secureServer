import sys, json, time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent.parent))

from SecureServer.code.file_handling import load_tokens
from SecureServer.code.logs import server_log
from SecureServer.code.token_handling import get_user
from SecureServer.adminPortal.adminlogin import authenticate_session

def sanitize_safe_tokens(raw_tokens):
    """Return safe to log fields (not passwords, secrets, entire vaults, etc)"""
    sanitized = []

    for t in raw_tokens:
        user = get_user(t.get("user_id"))

        sanitized.append({
            "session_id": t.get("session_id"),
            "value": t.get("safe_log"),
            "username": user["username"] if user else "<user removed>",
            "login_time": time.strftime(
                "%Y-%m-%d %H:%M:%S",
                time.localtime(t.get("auth_time"))
            ) if t.get("auth_time") else None,
            "user_id": t.get("user_id")
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

    server_log("COMMAND", f"{user['username']} requested list of active sessions")

    # ---- Print sessions ----
    tokens = load_tokens()
    output = sanitize_safe_tokens(tokens)

    print(json.dumps(output))
    sys.exit(0)
