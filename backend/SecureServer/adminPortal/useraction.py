import sys, json
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent.parent.parent))
from SecureServer.code.file_handling import load_failed_attempts, save_failed_attempts, load_users, save_users
from SecureServer.code.logs import server_log
from SecureServer.adminPortal.adminlogin import authenticate_session


if __name__ == "__main__":
    # ---- Args ----
    if len(sys.argv) != 4:
        print("Invalid arguments", file=sys.stderr)
        sys.exit(2)

    session_id = sys.argv[1]
    action = sys.argv[2]
    user_id = sys.argv[3]

    # ---- Auth ----
    user = authenticate_session(session_id)
    if not user:
        print("Invalid session", file=sys.stderr)
        sys.exit(1)

    # ---- Run Command ----
    all_users = load_users()

    print("AVAILABLE USERS:")
    for u in all_users:
        print(u["username"], u["id"])

    edit = next((u for u in all_users if u["id"] == user_id), None)
    if not edit:
        print("Invalid user ID", file=sys.stderr)
        sys.exit(1)
    
    if action == "freeze":
        edit["freeze"] = True
        server_log("COMMAND", f"{user['username']} froze all actions for user {edit['username']}.")
    elif action == "unfreeze":
        edit["freeze"] = False
        server_log("COMMAND", f"{user['username']} unfroze available actions for user {edit['username']}.")
    elif action == "clear_attempts":
        failed_attempts = load_failed_attempts()
        if edit["username"] in failed_attempts:
            failed_attempts.pop(edit["username"], None)
            save_failed_attempts(failed_attempts)
            server_log("COMMAND", f"{user['username']} cleared failed attempts for '{edit['username']}'.")
        sys.exit(0)
    elif action == "promote_app_admin":
        edit["admin"] = True
        server_log("COMMAND", f"{user['username']} promoted user {edit['username']} to app admin.")
    elif action == "demote_app_admin":
        edit["admin"] = False
        server_log("COMMAND", f"{user['username']} revoked all app admin privileges from user {edit['username']}.")
    elif action == "promote_dev_admin":
        edit["dev_admin"] = True
        server_log("COMMAND", f"{user['username']} promoted user {edit['username']} to developer admin.")
    elif action == "demote_dev_admin":
        edit["dev_admin"] = False
        server_log("COMMAND", f"{user['username']} revoked all developer admin privileges from user {edit['username']}.")
    elif action == "grant_root_auth":
        edit["root_auth"] = True
        server_log("COMMAND", f"{user['username']} granted user {edit['username']} root command access.")
    elif action == "revoke_root_auth":
        edit["root_auth"] = False
        server_log("COMMAND", f"{user['username']} revoked all root command access from user {edit['username']}.")
    else:
        server_log("ERROR", f"{user['username']} tried to execute unknown user command: {action}")
        sys.exit(1)

    save_users(all_users)
    sys.exit(0)