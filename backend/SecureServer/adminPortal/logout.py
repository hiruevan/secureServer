import sys, json
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent.parent.parent))
from SecureServer.code.file_handling import load_tokens, save_tokens
from SecureServer.code.logs import server_log
from SecureServer.adminPortal.adminlogin import authenticate_session

if __name__ == "__main__":
    # ---- Args ----
    if len(sys.argv) != 3:
        print("Invalid arguments", file=sys.stderr)
        sys.exit(2)

    session_id = sys.argv[1]
    target_user_id  = sys.argv[2]

    # ---- Auth ----
    user = authenticate_session(session_id)
    if not user:
        print("Invalid session", file=sys.stderr)
        sys.exit(1)

    # ---- Logout all user_id's sessions ----
    tokens = load_tokens()
    if len(tokens) == 0:
        print("No active sessions", file=sys.stderr)
        sys.exit(1)

    new_tokens = []
    removed_count = 0

    for token in tokens:
        # token is expected to contain a user id
        if str(token.get("user_id")) == str(target_user_id):
            removed_count += 1
            continue
        new_tokens.append(token)

    save_tokens(new_tokens)
    server_log("COMMAND",f"{user['username']} logged out {removed_count} session(s) for user id {target_user_id}.")

    sys.exit(0)