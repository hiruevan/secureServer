import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent.parent))

from SecureServer.code.file_handling import load_tokens, save_tokens
from SecureServer.code.logs import server_log
from SecureServer.adminPortal.adminlogin import authenticate_session

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

    tokens = load_tokens() or []

    if not tokens:
        print("No active sessions", file=sys.stderr)
        sys.exit(1)

    user_id = user["id"]

    new_tokens = []
    removed_count = 0

    for token in tokens:
        if token.get("user_id") == user_id:
            removed_count += 1
            continue
        new_tokens.append(token)

    save_tokens(new_tokens)

    server_log("LOGOUT", f"Dev Admin {user['username']} logged out {removed_count} session(s) for self.")

    sys.exit(0)
