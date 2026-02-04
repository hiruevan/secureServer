from pathlib import Path

CODE = Path(__file__).parent
BACKEND = CODE.parent
EXE_PATH = BACKEND.parent
CONSOLE_PATH = BACKEND / "console.py"
SERVER_PATH = BACKEND / "server.py"
DATA = BACKEND / "data"
BASE_DIR = BACKEND.parent
FRONTEND = BASE_DIR / "frontend"
USERS_FILE = DATA / "users.json"
TOKENS_FILE = DATA / "tokens.json"
FAILED_LOGINS_FILE = DATA / "failed_attempts.json"
SERVER_LOGS_FILE = BACKEND / "server.log"
ENV_FILE = EXE_PATH / ".env"
PID_FILE = BACKEND / "server.pid"