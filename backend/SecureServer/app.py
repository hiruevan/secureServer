import os, time, pyotp, uuid, sys, copy

from functools import wraps

from SecureServer.code.token_handling import verify_csrf, require_token, safe_token_log, get_new_token, remove_all_tokens
from SecureServer.code.logs import server_log
from SecureServer.code.file_handling import load_failed_attempts, save_failed_attempts, load_users, save_users, load_encrypted_json, write_encrypted_json
from SecureServer.code.encryption import verify_pw, hash_pw

from twilio.rest import Client
import smtplib
from email.mime.text import MIMEText
from email.mime.multipart import MIMEMultipart

from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse

from slowapi import Limiter, _rate_limit_exceeded_handler
from slowapi.errors import RateLimitExceeded
from slowapi.util import get_remote_address

from fastapi.middleware.trustedhost import TrustedHostMiddleware
from starlette.middleware.sessions import SessionMiddleware
from slowapi.middleware import SlowAPIMiddleware
from SecureServer.code.middleware import SecurityHeadersMiddleware, HTTPSRedirectMiddleware, StaticFilesWithHeaders

from SecureServer.code.environment_variables import (
    LOCKOUT_LOGIN_WINDOW, PW_CHANGE_AUTH_WINDOW, MAX_LOGIN_FAILURES, TOKEN_AGE,
    APP_NAME, ALLOWED_HOSTS, USE_HTTPS, SYSTEM_KEY,
    ENABLE_2FA, REQUIRE_2FA,
    DEFAULT_USER_2FA, DEFAULT_USER_TAKE_FULL_NAME,
    DEFAULT_USER_TAKE_EMAIL, DEFAULT_USER_TAKE_PHONE,
    SMTP_SERVER, SMTP_PORT, SMTP_USERNAME, SMTP_PASSWORD, FROM_EMAIL,
    TWILIO_ACCOUNT_SID, TWILIO_AUTH_TOKEN, TWILIO_PHONE_NUMBER
)

class Database:
    def __init__(self):
        pass

    def log(self, message: str, extra: str) -> None:
        server_log(message, extra)
    
    def load_json(self, file: str, is_dict: bool = False):
        return load_encrypted_json(file, is_dict)
    
    def write_json(self, file: str, data: dict) -> None:
        write_encrypted_json(file, data)

    def load_users(self):
        return load_users()
    
    def save_users(self, users):
        save_users(users)

class DefaultUser:
    keys: list
    defaults: list

    DEFAULT_2FA: bool
    TAKE_FULL_NAME: bool
    TAKE_EMAIL: bool
    TAKE_PHONE: bool

    def __init__(self):
        self.keys = []
        self.defaults = []

        self.DEFAULT_2FA = DEFAULT_USER_2FA
        self.TAKE_FULL_NAME = DEFAULT_USER_TAKE_FULL_NAME
        self.TAKE_EMAIL = DEFAULT_USER_TAKE_EMAIL
        self.TAKE_PHONE = DEFAULT_USER_TAKE_PHONE

    def add(self, key: str, default = "") -> None:
        self.keys.append(key)
        self.defaults.append(default)

    def _has_contact(self) -> bool:
        return self.TAKE_EMAIL or self.TAKE_PHONE

class SecureApp:
    main: FastAPI
    database: Database
    DEFAULT_USER: DefaultUser
    ALLOWED_HOSTS: list

    _limiter: Limiter
    _has_middleware: bool = False

    def __init__(self):
        self.main = FastAPI()
        self.database = Database()
        self.DEFAULT_USER = DefaultUser()
        self._limiter = Limiter(key_func=get_remote_address)

        self.main.state.limiter = self._limiter
        self.main.add_exception_handler(RateLimitExceeded, _rate_limit_exceeded_handler)

    def add_security_headers(self) -> None:
        self._has_middleware = True
        self.main.add_middleware(TrustedHostMiddleware, allowed_hosts=ALLOWED_HOSTS)
        self.main.add_middleware(SessionMiddleware, secret_key=SYSTEM_KEY)
        self.main.add_middleware(SecurityHeadersMiddleware)
        self.main.add_middleware(SlowAPIMiddleware)

        if USE_HTTPS:
            self.main.add_middleware(HTTPSRedirectMiddleware)

    def add_middleware(self, middleware, *args, **kwargs) -> None:
        self._has_middleware = True
        self.main.add_middleware(middleware, *args, **kwargs)

    def mount(self, directory: str) -> None:
        if not self._has_middleware:
            self.database.log("WARNING", "No middleware was added to the app. This is a security issue.")
        self.main.mount("/", StaticFilesWithHeaders(directory=directory, html=True), name="frontend")
    
    def get(self, path: str, *args, **kwargs):
        return self.main.get(path, *args, **kwargs)

    def post(self, path: str, *args, **kwargs):
        return self.main.post(path, *args, **kwargs)

    def limit(self, limit_string: str):
        return self._limiter.limit(limit_string)
    

    def auth_guard(self, admin: bool = False, csrf: bool = True):
        """
        Decorator for token and authentication required routes
        """
        def decorator(func):
            @wraps(func)
            async def wrapper(*args, **kwargs):
                # Find the FastAPI Request object
                request = None
                for value in list(args) + list(kwargs.values()):
                    if hasattr(value, "headers") and hasattr(value, "url"):
                        request = value
                        break

                if request is None:
                    return JSONResponse({
                        "success": False,
                        "message": "Request object not found"
                    })
                
                try:
                    # ---- Token Required ----
                    token_request = require_token(request)
                    if not token_request["success"]:
                        return JSONResponse(token_request)

                    user = token_request["user"]
                    token = token_request["token"]
                    key = token_request["key"]

                    if user.get("root", False):
                        server_log("NOTICE", f"Root user tried to log into webapp: {user['username']}") 
                        return JSONResponse({
                            "success": False,
                            "message": "That feature is not supported by this user account."
                        })

                    if not key:
                        return JSONResponse({
                            "success": False,
                            "message": "Authentication Key is required."
                        })
                    
                    if user.get("freeze", False):
                        server_log("SECURITY NOTICE", f"Frozen user tried to log in to webapp: {user['username']}") 
                        res = JSONResponse({
                            "success": False,
                            "message": "Your account is disabled."
                        })
                        res.delete_cookie("auth_token")
                        res.delete_cookie("auth_key")
                        res.delete_cookie("csrf_key")
                        return res

                    # ---- Admin Required ----
                    if admin and not user.get("admin", False):
                        return JSONResponse({
                            "success": False,
                            "message": "Admin privileges required."
                        })

                    # ---- CSRF Validation ----
                    if csrf:
                        csrf_result = verify_csrf(request, token)
                        if not csrf_result["success"]:
                            return JSONResponse(csrf_result)

                    # Everything OK – call original route handler
                    request.state.user = user
                    request.state.token = token
                    request.state.key = key

                    return await func(*args, **kwargs)

                except Exception as e:
                    server_log("ERROR", f"{func.__name__} exception: {str(e)}")
                    return JSONResponse({"success": False, "message": "An error occurred. Please try again."})
            
            wrapper.__name__ = func.__name__
            wrapper.__doc__ = func.__doc__
            return wrapper
        return decorator
 
    def login_guard(self):
        """
        Decorator for login routes with full optional 2FA support.
        """
        def decorator(func):
            @wraps(func)
            async def wrapper(*args, **kwargs):
                try:
                    # --- Find FastAPI Request object ---
                    request = None
                    for value in list(args) + list(kwargs.values()):
                        if hasattr(value, "headers") and hasattr(value, "url"):
                            request = value
                            break
                    if request is None:
                        return JSONResponse({"success": False, "message": "Request object not found"})

                    # --- Find login data (Pydantic model) ---
                    data = None
                    for value in list(args) + list(kwargs.values()):
                        if hasattr(value, "username") and hasattr(value, "password"):
                            data = value
                            break
                    if data is None:
                        return JSONResponse({"success": False, "message": "Login data not found"})

                    # --- Load Users ---
                    users = load_users()

                    # --- Load failed login attempts ---
                    failed_attempts = load_failed_attempts()
                    attempts = failed_attempts.get(data.username, [])
                    attempts = [ts for ts in attempts if time.time() - ts < LOCKOUT_LOGIN_WINDOW]
                    failed_attempts[data.username] = attempts

                    # --- Find user ---
                    user = next((u for u in users if u["username"] == data.username), None)

                    # --- Check lockout ---
                    if len(attempts) >= MAX_LOGIN_FAILURES:
                        remaining = int(LOCKOUT_LOGIN_WINDOW - (time.time() - min(attempts)))
                        server_log(
                            "SECURITY NOTICE",
                            f"Account locked for user {data.username} due to repeated failures."
                        )
                        save_failed_attempts(failed_attempts)
                        return JSONResponse({
                            "success": False,
                            "message": f"Account temporarily locked. Try again in {remaining // 60} minutes."
                        })
                    
                    if user.get("freeze", False):
                        server_log("SECURITY NOTICE", f"Frozen user tried to log in to webapp: {user['username']}") 
                        res = JSONResponse({
                            "success": False,
                            "message": "Your account is disabled."
                        })
                        res.delete_cookie("auth_token")
                        res.delete_cookie("auth_key")
                        res.delete_cookie("csrf_key")
                        return res
                    
                    if user.get("root", False): 
                        server_log("SECURITY NOTICE", f"Root user tried to log into webapp: {user['username']}") 
                        return JSONResponse({
                            "success": False,
                            "message": "Credentials do not match."
                        })
                    
                    # --- Dummy hash for timing-attack protection ---
                    if user:
                        target_hash = user["password"]
                    else:
                        # Create a deterministic but unpredictable dummy hash based on username
                        # This ensures the same dummy is used for the same (invalid) username
                        target_hash = hash_pw(data.username + "_dummy")
                    valid_password = verify_pw(data.password, target_hash)

                    user_exists = user is not None
                    credentials_valid = user_exists and valid_password

                    if not credentials_valid:
                        # Failed login
                        attempts.append(time.time())
                        failed_attempts[data.username] = attempts
                        save_failed_attempts(failed_attempts)
                        server_log("SECURITY NOTICE", f"Failed login for user {data.username}.")
                        return JSONResponse({"success": False, "message": "Credentials do not match."})

                    # --- Check 2FA ---
                    needs_2fa = (user.get("2fa_enabled", False) or REQUIRE_2FA) and ENABLE_2FA
                    totp_secret = user.get("2fa_secret", pyotp.random_base32())
                    totp = pyotp.TOTP(totp_secret)

                    if needs_2fa and not getattr(data, "totp_code", None):
                        if not user.get("2fa_setup_complete", False):
                            totp_uri = totp.provisioning_uri(
                                name=data.username,
                                issuer_name=APP_NAME
                            )
                            server_log("NOTICE", f"Sent user {data.username} 2FA activation code.")
                            return JSONResponse({
                                "success": True,
                                "require2FA": True,
                                "qr_data": totp_uri,
                                "message": "Scan this QR code with your authenticator app to enable 2FA."
                            })
                        else:
                            server_log("UPDATE", f"Prompted 2FA OTP for user {data.username}.")
                            return JSONResponse({
                                "success": True,
                                "require2FA": True,
                                "message": "Enter your 2FA code to continue."
                            })

                    # Always verify TOTP if code provided (even if 2FA not enabled)
                    # This prevents timing attacks revealing 2FA status
                    if getattr(data, "totp_code", None):
                        totp_valid = totp.verify(str(data.totp_code))
                        
                        if needs_2fa and not totp_valid:
                            server_log("SECURITY NOTICE", f"Failed 2FA for user {data.username}.")
                            return JSONResponse({"success": False, "message": "Invalid 2FA code."})
                        
                        if needs_2fa and not user.get("2fa_setup_complete", False):
                            server_log("UPDATE", f"2FA activation successful for {data.username}.")
                            user["2fa_setup_complete"] = True
                            save_users(users)

                    # --- Successful login ---
                    if data.username in failed_attempts:
                        del failed_attempts[data.username]
                        save_failed_attempts(failed_attempts)

                    # --- Generate token & cookies ---
                    token, key, csrf = get_new_token(user["id"], data.password, TOKEN_AGE)
                    server_log("LOGIN", f"Successful login for user {data.username}. Served token {safe_token_log(token)}.")

                    response = JSONResponse({"success": True, "message": "Successfully logged in."})
                    response.set_cookie(
                        key="auth_token",
                        value=token,
                        max_age=TOKEN_AGE,
                        httponly=True,
                        secure=USE_HTTPS,
                        samesite="strict"
                    )
                    response.set_cookie(
                        key="auth_key",
                        value=key,
                        max_age=TOKEN_AGE,
                        httponly=True,
                        secure=USE_HTTPS,
                        samesite="strict"
                    )
                    response.set_cookie(
                        key="csrf_token",
                        value=csrf,
                        max_age=TOKEN_AGE,
                        httponly=False,
                        secure=USE_HTTPS,
                        samesite="lax"
                    )

                    await func(*args, **kwargs)
                    return response
                except Exception as e:
                    server_log("ERROR", f"Login exception: {e}\n{type(e).__name__}")
                    return JSONResponse({"success": False, "message": "Login failed due to server error."})

            return wrapper
        return decorator

    def signup_guard(self):
        def decorator(func):
            @wraps(func)
            async def wrapper(*args, **kwargs):
                # Find FastAPI Request
                request = None
                for value in list(args) + list(kwargs.values()):
                    if hasattr(value, "headers") and hasattr(value, "url"):
                        request = value
                        break
                if request is None:
                    return JSONResponse({"success": False, "message": "Request object not found"})

                # Find data (Pydantic model or form)
                data = None
                for value in list(args) + list(kwargs.values()):
                    if hasattr(value, "username") and hasattr(value, "password"):
                        data = value
                        break
                if data is None:
                    return JSONResponse({"success": False, "message": "Signup data not found"})
                
                users = load_users()

                # Check if username already exists
                if any(u["username"] == data.username for u in users):
                    server_log("ERROR", f"Failed signup: username {data.username} already exists.")
                    return JSONResponse({"success": False, "message": "Username already exists."})
                
                # Get template
                template = next((u for u in users if u["username"] == "template"), None)
                if not template:
                    server_log("ERROR", f"{data.username} tried to sign up, but the template user was not found (try restarting the server).")
                    return JSONResponse({"success": False, "message": "Sever side error"})

                # Create a new user
                new_user = copy.deepcopy(template)

                new_user["id"] = str(uuid.uuid4())
                new_user["username"] = data.username
                new_user["password"] = hash_pw(data.password)
                new_user["salt"] = os.urandom(16).hex()
                new_user["2fa_secret"] = pyotp.random_base32()
                
                if self.DEFAULT_USER.TAKE_FULL_NAME:
                    new_user["first_name"] = data.first_name
                    new_user["last_name"] = data.last_name
                if self.DEFAULT_USER.TAKE_EMAIL:
                    new_user["email"] = data.email
                if self.DEFAULT_USER.TAKE_PHONE:
                    new_user["phone"] = data.phone

                # Append the new user to the database
                users = load_users()
                users.append(new_user)
                save_users(users)

                server_log("SIGNUP", f"Successful signup for new user {data.username}. Not an admin.")
                await func(*args, **kwargs)
                return JSONResponse({"success": True, "message": "User successfully created."})

            wrapper.__name__ = func.__name__
            wrapper.__doc__ = func.__doc__
            return wrapper
        return decorator
    

    def force_logout(self):
        def decorator(func):
            @wraps(func)
            async def wrapper(*args, **kwargs):
                # Find FastAPI Request
                request = None
                for value in list(args) + list(kwargs.values()):
                    if hasattr(value, "headers") and hasattr(value, "url"):
                        request = value
                        break
                if request is None:
                    return JSONResponse({"success": False, "message": "Request object not found"})
                
                try:
                    token_request = require_token(request)
                    user = token_request["user"]
                    remove_all_tokens(user["id"])

                    server_log("LOGOUT", f"User {user['username']} logged out and thier token was removed.")
                    response = JSONResponse({"success": True, "message": "Logged out successfully."})
                    response.delete_cookie("auth_token")
                    response.delete_cookie("auth_key")
                    response.delete_cookie("csrf_key")

                    await func(*args, **kwargs)

                    return response

                except Exception as e:
                    server_log("LOGOUT ERROR", f"{func.__name__}, {e}")
                    return JSONResponse({"success": False, "message": "Error durring logout."})
            return wrapper
        return decorator
    
    def change_pw_protocal(self):
        def decorator(func):
            @wraps(func)
            async def wrapper(*args, **kwargs):
                # Find FastAPI Request
                request = None
                for value in list(args) + list(kwargs.values()):
                    if hasattr(value, "headers") and hasattr(value, "url"):
                        request = value
                        break
                if request is None:
                    return JSONResponse({"success": False, "message": "Request object not found"})

                # Find data
                data = None
                for value in list(args) + list(kwargs.values()):
                    if hasattr(value, "old_password") and hasattr(value, "new_password"):
                        data = value
                        break
                if data is None:
                    return JSONResponse({"success": False, "message": "Newpassword data not found"})
                
                try:
                    token_request = require_token(request)
                    user = token_request["user"]
                    users = load_users()
                    user_record = next((u for u in users if u["id"] == user["id"]), None)
                    if not user_record:
                        server_log("ERROR", f"User record not found for {user['username']} during password change.")
                        return JSONResponse({"success": False, "message": "User data error."})

                    # Verify the current password
                    if not verify_pw(data.old_password, user_record["password"]):
                        server_log("SECURITY NOTICE", f"Failed password change for {user['username']} - wrong old password.")
                        return JSONResponse({"success": False, "message": "Incorrect current password."})
                    
                    # Re authenticate password
                    last_auth = token_request["token"].get("auth_time", 0)
                    if time.time() - last_auth > PW_CHANGE_AUTH_WINDOW:
                        return JSONResponse({
                            "success": False,
                            "message": "Please re-authenticate to change your password.",
                            "requires_login": True
                        })
                    
                    # Update password hash
                    user_record["password"] = hash_pw(data.new_password)

                    save_users(users)
                    server_log("PASSWORD CHANGE", f"Password successfully changed for user {user['username']}. Vault key re-wrapped.")

                    self.send_notification(user, "Password Changed", 
                        "Your password was recently changed. If this wasn't you, contact support immediately.")

                    await func(*args, **kwargs)

                    # Force logout
                    remove_all_tokens(user_record["id"])

                    server_log("LOGOUT", f"User {user_record['username']} logged out and thier token was removed.")
                    response = JSONResponse({"success": True, "message": "Password successfully changed. All sessions logged out."})
                    response.delete_cookie("auth_token")
                    response.delete_cookie("auth_key")
                    response.delete_cookie("csrf_key")

                    return response
                except Exception as e:
                    server_log("ERROR", f"pw change error: {func.__name__}, {e}")
                    return JSONResponse({"success": False, "message": "Error durring password change."})
            return wrapper
        return decorator
    

    def send_notification(self, user: dict, subject: str, message: str) -> bool:
        """
        Send notifications to users via email or SMS based on their preferences.
        
        Args:
            user: User dictionary containing contact info and preferences
            subject: Notification subject/title
            message: Notification message content
        """
        if not self.DEFAULT_USER._has_contact():
            return False
        
        email = user.get("email")
        phone = user.get("phone")
        method = user.get("preferred_contact_method")

        # Try preferred method first
        if method == "email" and email:
            try:
                self._send_email(email, subject, message)
                server_log("NOTIFICATION", f"Email sent to {user.get('username')}: {subject}")
                return True
            except Exception as e:
                server_log("ERROR", f"Failed to send email to {user.get('username')}: {e}")
                # Fall back to SMS if email fails
                if phone:
                    try:
                        self._send_sms(phone, f"{subject}: {message}")
                        server_log("NOTIFICATION", f"SMS sent to {user.get('username')} (email fallback)")
                        return True
                    except Exception as e2:
                        server_log("ERROR", f"Failed to send SMS fallback to {user.get('username')}: {e2}")
                        return False
        
        elif method == "sms" and phone:
            try:
                self._send_sms(phone, f"{subject}: {message}")
                server_log("NOTIFICATION", f"SMS sent to {user.get('username')}: {subject}")
                return True
            except Exception as e:
                server_log("ERROR", f"Failed to send SMS to {user.get('username')}: {e}")
                # Fall back to email if SMS fails
                if email:
                    try:
                        self._send_email(email, subject, message)
                        server_log("NOTIFICATION", f"Email sent to {user.get('username')} (SMS fallback)")
                        return True
                    except Exception as e2:
                        server_log("ERROR", f"Failed to send email fallback to {user.get('username')}: {e2}")
                        return False
        
        # No preferred method or it's not set - try email first, then SMS
        if email:
            try:
                self._send_email(email, subject, message)
                server_log("NOTIFICATION", f"Email sent to {user.get('username')}: {subject}")
                return True
            except Exception as e:
                server_log("ERROR", f"Failed to send email to {user.get('username')}: {e}")
                return False
        
        if phone:
            try:
                self._send_sms(phone, f"{subject}: {message}")
                server_log("NOTIFICATION", f"SMS sent to {user.get('username')}: {subject}")
                return True
            except Exception as e:
                server_log("ERROR", f"Failed to send SMS to {user.get('username')}: {e}")
                return False
        
        # No contact method available
        server_log("WARNING", f"No valid contact method for user {user.get('username')}")
        return False
    
    def _send_email(self, to_email: str, subject: str, body: str) -> None:
        """
        Send email notification using SMTP.
        Configure SMTP settings via environment variables.
        """
        # Use environment variables with validation
        if not SMTP_USERNAME or not SMTP_PASSWORD:
            raise ValueError("SMTP credentials not configured (SMTP_USERNAME and SMTP_PASSWORD required)")
        
        # Use FROM_EMAIL if set, otherwise fall back to SMTP_USERNAME
        from_email = FROM_EMAIL if FROM_EMAIL else SMTP_USERNAME
        
        msg = MIMEMultipart()
        msg['From'] = from_email
        msg['To'] = to_email
        msg['Subject'] = subject
        
        msg.attach(MIMEText(body, 'plain'))
        
        with smtplib.SMTP(SMTP_SERVER, SMTP_PORT) as server:
            server.starttls()
            server.login(SMTP_USERNAME, SMTP_PASSWORD)
            server.send_message(msg)
    
    def _send_sms(self, to_phone: str, message: str) -> None:
        """
        Send SMS notification using Twilio.
        Configure Twilio settings via environment variables.
        """
        # Use environment variables with validation
        if not TWILIO_ACCOUNT_SID or not TWILIO_AUTH_TOKEN or not TWILIO_PHONE_NUMBER:
            raise ValueError("Twilio credentials not configured (TWILIO_ACCOUNT_SID, TWILIO_AUTH_TOKEN, TWILIO_PHONE_NUMBER required)")
        
        client = Client(TWILIO_ACCOUNT_SID, TWILIO_AUTH_TOKEN)
        
        client.messages.create(
            body=message,
            from_=TWILIO_PHONE_NUMBER,
            to=to_phone
        )

    
    def cleanup_func(self):
        pass

    def _signal_handler(self, sig, frame):
        """Handle shutdown signals gracefully"""
        print("\n[SHUTDOWN] Received shutdown signal. Cleaning up...")
        self.database.log("SHUTDOWN", "Received shutdown signal. Cleaning up...")
        
        self.cleanup_func()
        
        self.database.log("SHUTDOWN", "Cleanup complete. Shutting down.")
        print("[SHUTDOWN] Cleanup complete. Shutting down.")
        sys.exit(0)