import sys, json, time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent.parent))

from SecureServer.code.file_handling import load_failed_attempts
from SecureServer.code.logs import server_log
from SecureServer.adminPortal.adminlogin import authenticate_session


def format_ts(ts):
    if ts is None:
        return None
    return time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(ts))


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

    server_log("COMMAND", f"{user['username']} requested list of failed attempts.")

    # ---- Load attempts ----
    failed_attempts = load_failed_attempts()
    output = []

    for username, timestamps in failed_attempts.items():
        # Entry that identifies the user
        output.append({
            "username": username,
            "timestamp": None
        })

        # Entries for each failed attempt
        for ts in timestamps:
            output.append({
                "username": username,
                "timestamp": format_ts(ts)
            })

    # ---- Output ----
    print(json.dumps(output))
    sys.exit(0)
