import ssl, time, sys, signal, os, atexit, pyotp, uuid, string, random
from uvicorn import Config, Server

from SecureServer.app import SecureApp

import SecureServer.code.encryption as en
from SecureServer.code.paths import PID_FILE
from SecureServer.code.request_validation import *
from SecureServer.code.environment_variables import (
    SERVER_HOST, SERVER_PORT, HTTPS_HOST, HTTPS_PORT, USE_HTTPS,
    ENABLE_2FA, REQUIRE_2FA,
    SSL_CERT_FILE, SSL_KEY_FILE, SSL_CIPHERS,
    TEMPLATE_USER_EMAIL, TEMPLATE_USER_PHONE,
    REPLACE_CORRUPTED_FILES
)

class Encryptor:
    def calculate_hmac(data: str) -> str:
        return en.calculate_hmac(data)

    def get_aes_key(key: str) -> bytes:
        return en.get_aes_key(key)

    def derive_vault_key(password: str, salt_hex: str) -> bytes:
        """Derives KEK from password - used to wrap/unwrap vault master key"""
        return en.derive_vault_key(password, salt_hex)
    
    def generate_vault_master_key() -> str:
        """Generates a new random vault master key"""
        return en.generate_vault_master_key()
    
    def wrap_vault_key(master_key: str, kek: str) -> str:
        """Wraps (encrypts) vault master key with password-derived KEK"""
        return en.wrap_vault_key(master_key, kek)
    
    def unwrap_vault_key(wrapped_key: str, kek: str) -> str:
        """Unwraps (decrypts) vault master key using password-derived KEK"""
        return en.unwrap_vault_key(wrapped_key, kek)

    def encrypt_vault(data: str, key: str) -> str:
        """Encrypts vault data with master key"""
        return en.encrypt_vault(data, key)
    
    def decrypt_vault(enc_data: str, key: str) -> str:
        """Decrypts vault data with master key"""
        return en.decrypt_vault(enc_data, key)

    def hash_pw(password: str) -> str:
        return en.hash_pw(password)

    def verify_pw(password: str, stored: str) -> bool:
        return en.verify_pw(password, stored)

    def basic_hash(string: str) -> str:
        return en.basic_hash(string)
    
    def url_b64decode(data: str) -> bytes:
        return en.urlsafe_b64decode(data)

    def urlsafe_b64decode_padded(data: str) -> bytes:
        return en.urlsafe_b64decode_padded(data)
    
    def random_base32() -> str:
        return en.random_base32()

class SecureServer:
    app: SecureApp
    port: int
    host: str
    cert_file: str
    key_file: str

    _config: Config
    _server: Server
    
    def __init__(self):
        self.port = SERVER_PORT
        self.host = SERVER_HOST

    def LoadConfig(self) -> Config:
        # Register the signal handler for graceful shutdown
        signal.signal(signal.SIGINT, self._signal_handler)
        signal.signal(signal.SIGTERM, self._signal_handler)

        if USE_HTTPS:
            if not SSL_CERT_FILE or not SSL_KEY_FILE:
                print("CRITICAL", "SSL_CERT_FILE and SSL_KEY_FILE must be set in production")
                time.sleep(1)
                sys.exit(1)
            self._config = Config(
                self.app.main,
                host=HTTPS_HOST,
                port=HTTPS_PORT,
                ssl_certfile=SSL_CERT_FILE,
                ssl_keyfile=SSL_KEY_FILE,
                ssl_version=ssl.PROTOCOL_TLS_SERVER,
                ssl_ciphers=SSL_CIPHERS,
                log_config=None
            )
        else: 
            self._config = Config(
                self.app.main,
                host=SERVER_HOST,
                port=SERVER_PORT,
                log_config=None
            )
        
        self._server = Server(self._config)
        return self._config
    
    def _generate_unique_char_string(self, length: int) -> str:
        characters = string.ascii_letters + string.digits
        
        # Check if the requested length is possible with the available characters
        if length > len(characters):
            return "Error: Length requested is more than available unique characters."

        # Randomly sample unique characters for the specified length and join them
        unique_string = ''.join(random.sample(characters, k=length))
        
        return unique_string
    def _cleanup_pid(self):
        """Clean up PID file on shutdown"""
        try:
            if os.path.exists(PID_FILE):
                os.remove(PID_FILE)
        except Exception as e:
            print(f"Error cleaning up PID file: {e}")
    def _signal_handler(self, sig, frame):
        """Handle shutdown signals gracefully"""
        self._cleanup_pid()
        self.app._signal_handler(sig, frame)
    def _create_template_user(self) -> dict:
        self.app.database.log("NOTICE", "Created template user for later user creation.")
        new_user = {
            "id": str(uuid.uuid4()),
            "username": "template",
            "password": Encryptor.hash_pw(self._generate_unique_char_string(72)),
            "admin": False,
            "salt": os.urandom(16).hex(),
            "vault": "",
            "root_auth": False,
            "dev_admin": False,
            "2fa_enabled": (self.app.DEFAULT_USER.DEFAULT_2FA or REQUIRE_2FA) and ENABLE_2FA,
            "2fa_secret": pyotp.random_base32(),
            "2fa_set_up_complete": False,
            "frozen": False,
        }
        if self.app.DEFAULT_USER.TAKE_FULL_NAME:
            new_user["first_name"] = "first"
            new_user["last_name"] = "last"
        if self.app.DEFAULT_USER.TAKE_EMAIL:
            new_user["email"] = "email@example.com"
            new_user["preferred_contact_method"] = "email"
        if self.app.DEFAULT_USER.TAKE_PHONE:
            new_user["phone"] = "1234567890"
            new_user["preferred_contact_method"] = "sms"

        for i in range(len(self.app.DEFAULT_USER.keys)):
            new_user[self.app.DEFAULT_USER.keys[i]] = self.app.DEFAULT_USER.defaults

        return new_user

    def run(self) -> None:
        # Warning notice for REPLACE_CORRUPTED_FILES
        if REPLACE_CORRUPTED_FILES:
            self.app.database.log("WARNING", "REPLACE_CORRUPTED_FILES is marked as True, this should only be toggled if debugging.")
            
        # Make sure there is a template user
        users = self.app.database.load_users()
        template = next((u for u in users if u["username"] == "template"), None)
        if not template:
            new = self._create_template_user()
            users.append(new)
            self.app.database.save_users(users)
                
        try:
            # Write PID file
            with open(PID_FILE, 'w') as f:
                f.write(str(os.getpid()))
            
            # Register cleanup to run on normal exit
            atexit.register(self._cleanup_pid)

            # Run the server
            self._server.run()
        except Exception as e:
            self.app.database.log("ERROR", f"Server run error: {e}")
        finally:
            # Ensure cleanup happens even if server crashes
            self._cleanup_pid()