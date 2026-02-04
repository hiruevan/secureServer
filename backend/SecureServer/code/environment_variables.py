import hashlib, os, base64, sys
from dotenv import load_dotenv
from SecureServer.code.logs import server_log
from SecureServer.code.paths import ENV_FILE

def get_bool_env(name: str, default: bool) -> bool:
    raw = os.environ.get(name)
    if raw is None:
        return default
    return raw.lower() in ("true", "1", "yes")

def get_int_env(name: str, default: int) -> int:
    raw = os.environ.get(name)
    try:
        return int(raw)
    except (TypeError, ValueError):
        return default

def get_str_env(name: str, default: str) -> str:
    raw = os.environ.get(name)
    if raw is None:
        return default
    # Strip matching quotes (single or double) from start and end
    if (raw.startswith('"') and raw.endswith('"')) or \
       (raw.startswith("'") and raw.endswith("'")):
        raw = raw[1:-1]
    return raw

def get_list_env(name: str, default: list, separator: str = ",") -> list:
    """Get a comma-separated list from environment variable"""
    raw = os.environ.get(name)
    if raw is None:
        return default
    # Strip quotes if present
    if (raw.startswith('"') and raw.endswith('"')) or \
       (raw.startswith("'") and raw.endswith("'")):
        raw = raw[1:-1]
    # Split and strip whitespace from each item
    return [item.strip() for item in raw.split(separator) if item.strip()]

def set_env_str(key: str, value: str) -> str:
    os.environ[key] = value
    update_env_file(key, value)  # persist in .env
    return value

def set_env_bool(key: str, value: str) -> bool:
    norm = value.lower()
    if norm not in ("true", "false", "1", "0", "yes", "no"):
        raise ValueError("Invalid boolean value (must be true/false)")
    os.environ[key] = norm
    update_env_file(key, norm)
    return norm in ("true", "1", "yes")

def set_env_int(key: str, value: str) -> int:
    try:
        iv = int(value)
    except ValueError:
        raise ValueError("Invalid integer value")
    os.environ[key] = value
    return iv

def get_env_value(var: str):
    """Return the current live value of an environment variable."""
    raw = os.environ.get(var)
    return raw

def update_env_file(var: str, value: str, env_path=ENV_FILE):
    """Permanently update an environment variable in the .env file."""
    try:
        with open(env_path, "r") as f:
            lines = f.readlines()
    except FileNotFoundError:
        lines = []

    new_lines = []
    found = False

    for line in lines:
        if line.strip().startswith(f"{var}="):
            new_lines.append(f"{var}={value}\n")
            found = True
        else:
            new_lines.append(line)

    if not found:
        new_lines.append(f"{var}={value}\n")

    with open(env_path, "w") as f:
        f.writelines(new_lines)

# --- For required cryptographic keys ---
def get_required_env_key(key_name: str) -> str:
    """Get a required cryptographic key from environment or fail."""
    value = os.environ.get(key_name)
    if not value:
        raise ValueError(
            f"CRITICAL: {key_name} environment variable not set. "
            f"Generate a secure key with: python -c \"import os, base64; print(base64.urlsafe_b64encode(os.urandom(32)).decode())\""
            f"Set an environment variable with: setx {key_name} \"value\""
        )
    if len(value) < 32:
        raise ValueError(f"CRITICAL: {key_name} must be at least 32 characters")
    # Hash the provided key to ensure consistent 32-byte length
    return base64.urlsafe_b64encode(hashlib.sha256(value.encode()).digest()).decode()

# --- Load environment file first ---
load_dotenv(ENV_FILE, override=True)

# --- Protected environment variables (REQUIRED) ---
try:
    SYSTEM_KEY = get_required_env_key("SYSTEM_KEY")  # Server-side main system key
    INTEGRITY_KEY = get_required_env_key("INTEGRITY_KEY")  # Server-side integrity check key
    ENCAPSILATION_KEY = get_required_env_key("ENCAPSILATION_KEY")  # Server-side encapsilation key
    TOKEN_KEY = get_required_env_key("TOKEN_KEY")  # Server-side token encryption key
except ValueError as e:
    print(f"FATAL ERROR: {e}")
    sys.exit(1)

# --- Application Configuration ---
APP_NAME = get_str_env("APP_NAME", "YourAppName")

# --- Server Configuration ---
SERVER_HOST = get_str_env("SERVER_HOST", "127.0.0.1")
SERVER_PORT = get_int_env("SERVER_PORT", 8000)
HTTPS_HOST = get_str_env("HTTPS_HOST", "0.0.0.0")
HTTPS_PORT = get_int_env("HTTPS_PORT", 443)

# --- SSL/TLS Configuration ---
SSL_CERT_FILE = get_str_env("SSL_CERT_FILE", "")
SSL_KEY_FILE = get_str_env("SSL_KEY_FILE", "")
SSL_CIPHERS = get_str_env("SSL_CIPHERS", "TLS_AES_256_GCM_SHA384:TLS_CHACHA20_POLY1305_SHA256")

# --- Allowed Hosts (parse comma-separated string) ---
ALLOWED_HOSTS = get_list_env("ALLOWED_HOSTS", ["localhost", "127.0.0.1", "0.0.0.0"])

# --- Authentication & Security ---
REPLACE_CORRUPTED_FILES = get_bool_env("REPLACE_CORRUPTED_FILES", True)  # Allow rewriting corrupted encrypted files
USE_HTTPS = get_bool_env("USE_HTTPS", False)  # Force HTTPS in production
LOCKOUT_LOGIN_WINDOW = get_int_env("LOCKOUT_LOGIN_WINDOW", 900)  # Lockout duration in seconds
PW_CHANGE_AUTH_WINDOW = get_int_env("PW_CHANGE_AUTH_WINDOW", 120)  # Password change re-authentication time window in seconds
MAX_LOGIN_FAILURES = get_int_env("MAX_LOGIN_FAILURES", 5)  # Failed login attempts before lockout
TOKEN_AGE = get_int_env("TOKEN_AGE", 900)  # Token lifetime in seconds

# --- 2FA Configuration ---
ENABLE_2FA = get_bool_env("ENABLE_2FA", False)  # Enable 2FA functionality
REQUIRE_2FA = get_bool_env("REQUIRE_2FA", False)  # Require 2FA for all users

# --- Default User Settings ---
DEFAULT_USER_2FA = get_bool_env("DEFAULT_USER_2FA", False)  # Enable 2FA by default for new users
DEFAULT_USER_TAKE_FULL_NAME = get_bool_env("DEFAULT_USER_TAKE_FULL_NAME", True)  # Collect full name during signup
DEFAULT_USER_TAKE_EMAIL = get_bool_env("DEFAULT_USER_TAKE_EMAIL", False)  # Collect email during signup
DEFAULT_USER_TAKE_PHONE = get_bool_env("DEFAULT_USER_TAKE_PHONE", False)  # Collect phone during signup

# --- Template User Defaults ---
TEMPLATE_USER_EMAIL = get_str_env("TEMPLATE_USER_EMAIL", "email@example.com")
TEMPLATE_USER_PHONE = get_str_env("TEMPLATE_USER_PHONE", "1234567890")

# --- Email Configuration (SMTP) ---
SMTP_SERVER = get_str_env("SMTP_SERVER", "smtp.gmail.com")
SMTP_PORT = get_int_env("SMTP_PORT", 587)
SMTP_USERNAME = get_str_env("SMTP_USERNAME", "")
SMTP_PASSWORD = get_str_env("SMTP_PASSWORD", "")
FROM_EMAIL = get_str_env("FROM_EMAIL", "")  # If empty, will use SMTP_USERNAME

# --- SMS Configuration (Twilio) ---
TWILIO_ACCOUNT_SID = get_str_env("TWILIO_ACCOUNT_SID", "")
TWILIO_AUTH_TOKEN = get_str_env("TWILIO_AUTH_TOKEN", "")
TWILIO_PHONE_NUMBER = get_str_env("TWILIO_PHONE_NUMBER", "")