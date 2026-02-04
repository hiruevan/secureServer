from SecureServer.code.file_handling import load_users, save_users
from SecureServer.code.logs import server_log

def make_admin(user_id: str) -> bool:
    """Makes a user an admin. Returns True if successful."""
    users = load_users()
    user = next((u for u in users if u["id"] == user_id), None)
    if not user:
        server_log("WARNING", f"Attempted to promote non-existent user {user_id}")
        return False
    user["is_admin"] = True
    save_users(users)
    server_log("UPDATE", f"User {user['username']} has been promoted to admin.")
    return True
def make_not_admin(user_id: str):
    """Makes a user not an admin. Returns True if successful."""
    users = load_users()
    user = next((u for u in users if u["id"] == user_id), None)
    if not user:
        server_log("WARNING", f"Attempted to demote non-existent user {user_id}")
        return False
    user["is_admin"] = False
    save_users(users)
    server_log("UPDATE", f"User {user['username']} has been made not an admin.")
    return True