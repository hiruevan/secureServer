import os, time, math
from pathlib import Path

from SecureServer.code.request_validation import SignupRequest, LoginRequest, VaultUpdateRequest, PasswordChangeRequest

from SecureServer.app import SecureApp, Request, JSONResponse
from SecureServer.server import *

BACKEND = Path(__file__).parent
BASE_DIR = BACKEND.parent
FRONTEND = BASE_DIR / "frontend"

app = SecureApp()
server = SecureServer()

app.NAME = "Secure Vault"

app.USE_HTTPS = False
app.ENABLE_2FA = True
app.REQUIRE_2FA = True
app.add_security_headers()

# === API Endpoints ===

# --- POSTs ---
@app.post("/signup") # ------ /signup
@app.limit("10/minute")
@app.signup_guard()
async def signup(request: Request, data: SignupRequest) -> JSONResponse:
    pass

@app.post("/login") # ------ /login
@app.limit("6/minute")
@app.login_guard()
async def login(request: Request, data: LoginRequest) -> JSONResponse:
    pass

@app.post("/logout") # ------ /logout
@app.limit("10/minute")
@app.auth_guard()
@app.force_logout()
async def logout(request: Request):
    pass

@app.post("/enable_2fa") # ------ /enable_2fa
@app.limit("6/hour")
@app.auth_guard()
async def enable_2fa(request: Request, data: dict) -> JSONResponse:
    users = app.database.load_users()
    user = request.state.user

    if user.get("2fa_enabled", False):
        return JSONResponse({"success": False, "message": "2FA is already enabled."})

    # Generate a new TOTP secret
    secret = Encryptor.random_base32()
    user["2fa_secret"] = secret

    app.database.save_users(users)

    return JSONResponse({
        "success": True,
        "message": "2FA turned on, you will be promted to activate it the next time you log in."
    })

@app.post("/disable_2fa") # ------ /disable_2fa
@app.limit("1/hour")
@app.auth_guard()
async def disable_2fa(request: Request, data: dict) -> JSONResponse:
    user = request.state.user
    user["2fa_secret"] = None
    user["2fa_enabled"] = False

    users = app.database.load_users()
    for i, u in enumerate(users):
        if u["id"] == user["id"]:
            users[i] = user
            break
    app.database.save_users(users)
    return JSONResponse({"success": True, "message": "2FA disabled."})

@app.post("/set_vault_information") # ------ /set_vault_information
@app.limit("3/minute")
@app.auth_guard()
async def set_vault_information(request: Request, data: VaultUpdateRequest) -> JSONResponse:
    user = request.state.user
    key = request.state.key

    users = app.database.load_users()
    val_user = next((u for u in users if u["id"] == user["id"]), None)
    if not val_user:
        app.database.log("ERROR", f"Connected user {user['username']} not found with token.")
        return JSONResponse({"success": False, "message": "User data error."})

    # Get or generate vault master key
    if not val_user.get("vault_master_key_wrapped"):
        # First time: generate new master key and wrap it
        master_key = Encryptor.generate_vault_master_key()
        wrapped_key = Encryptor.wrap_vault_key(master_key, key)
        val_user["vault_master_key_wrapped"] = wrapped_key
        app.database.log("SECURITY", f"Generated new vault master key for {user['username']}.")
    else:
        # Unwrap existing master key
        try:
            master_key = Encryptor.unwrap_vault_key(val_user["vault_master_key_wrapped"], key)
        except Exception as e:
            app.database.log("ERROR", f"Failed to unwrap vault key for {user['username']}: {e}")
            return JSONResponse({"success": False, "message": "Failed to decrypt vault key."})

    # Encrypt vault data with master key
    val_user["vault"] = Encryptor.encrypt_vault(data.data, master_key)
    app.database.save_users(users)
    app.database.log("UPDATE", f"User {user['username']} updated their vault (encrypted).")
    return JSONResponse({"success": True, "message": "Vault successfully updated and encrypted."})

@app.post("/change_password") # ------ /change_password
@app.limit("3/week")
@app.auth_guard()
@app.change_pw_protocal()
async def change_password(request: Request, data: PasswordChangeRequest) -> JSONResponse:
    pass

# --- GETs ---
@app.get("/get_personal_information") # ------ /get_personal_information
@app.limit("5/minute")
@app.auth_guard()
async def get_personal_information(request: Request) -> JSONResponse:
    user = request.state.user
    key = request.state.key

    vault = ""
    if user.get("vault"):
        try:
            # Unwrap the vault master key using password-derived KEK
            if not user.get("vault_master_key_wrapped"):
                vault = "[No vault key configured]"
            else:
                master_key = Encryptor.unwrap_vault_key(user["vault_master_key_wrapped"], key)
                vault = Encryptor.decrypt_vault(user["vault"], master_key)
        except Exception as e:
            vault = f"[Error decrypting vault: {str(e)}]"

    information = {
        "username": user["username"],
        "first_name": user["first_name"],
        "last_name": user["last_name"],
        "vault": vault
    }
    app.database.log("UPDATE", f"User {user['username']} requested personal info.")
    return JSONResponse({"success": True, "message": "Personal information served.", "information": information})

@app.get("/get_all_users") # ------ /get_all_users
@app.limit("5/minute")
@app.auth_guard(admin=True)
async def get_all_users(request: Request) -> JSONResponse:
    user = request.state.user
    users = app.database.load_users()
    safe_users = [
        {
            "id": u["id"],
            "username": u["username"],
            "name": f"{u.get('first_name')} {u.get('last_name')}",
            "admin": u.get("admin", False),
            "vault_size": len(u.get("vault", "")),
        } for u in users
    ]
    app.database.log("ADMIN ACTION", f"Admin {user['username']} retrieved all user data safely.")
    return JSONResponse({"success": True, "message": "All safe user data has been served.", "users": safe_users})

# --- Mount frontend & Run server ---
if __name__ == "__main__":
    app.mount(FRONTEND)
    
    server.app = app
    
    server.LoadConfig()
    app.database.log("STARTUP", "Server starting...")
    server.run()