import sys, uuid, os, pyotp, copy
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent.parent.parent))
from SecureServer.code.file_handling import load_users, save_users
from SecureServer.code.logs import server_log
from SecureServer.adminPortal.adminlogin import authenticate_session
from SecureServer.code.encryption import hash_pw

def auto_cast(value: str):
    v = value.strip().lower()

    # bool
    if v == "true":
        return True
    if v == "false":
        return False

    # null / none
    if v in ("null", "none"):
        return None

    # int
    if v.isdigit() or (v.startswith("-") and v[1:].isdigit()):
        return int(v)

    # float
    try:
        return float(value)
    except ValueError:
        pass

    # string fallback
    return value

if __name__ == "__main__":
    # ---- Args ----
    if len(sys.argv) < 4 or len(sys.argv) % 2 != 0:
        print("Invalid arguments", file=sys.stderr)
        sys.exit(2)

    session_id = sys.argv[1]
    username = sys.argv[2]
    password = sys.argv[3]

    custom_dict = sys.argv[4:] # Get custom set keys and their values

    # ---- Auth ----
    user = authenticate_session(session_id)
    if not user:
        print("Invalid session", file=sys.stderr)
        sys.exit(1)

    # ---- Create user ----
    users = load_users()
    template = next((u for u in users if u["username"] == "template"), None)
    if not template:
        server_log("ERROR", f"{user['username']} tried to create a user, but the template user was not found (try restarting the server).")
        sys.exit(1)

    server_log("COMMAND", f"{user['username']} created a new user: '{username}' (defaults from template).")
    new_user = copy.deepcopy(template)

    # Load into template
    new_user["id"] = str(uuid.uuid4())
    new_user["username"] = username
    new_user["password"] = hash_pw(password)
    new_user["salt"] = os.urandom(16).hex()
    new_user["2fa_secret"] = pyotp.random_base32()

    # Load custom data
    for i in range(0, len(custom_dict), 2):
        key = custom_dict[i]
        raw_value = custom_dict[i + 1]
        new_user[key] = auto_cast(raw_value)


    print(new_user)

    users.append(new_user)
    save_users(users)

    sys.exit(0)