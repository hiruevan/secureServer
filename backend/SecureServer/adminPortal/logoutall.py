import sys, json
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent.parent.parent))
from SecureServer.code.file_handling import save_tokens
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

    # ---- Clear tokens ----
    server_log("COMMAND", f"{user['username']} logged out all sessions.")

    save_tokens([])

    sys.exit(0)