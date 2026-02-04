import logging, re
from logging.handlers import RotatingFileHandler
from SecureServer.code.paths import SERVER_LOGS_FILE

IGNORED_LOG_PATTERNS = [
    "CTRL+C", # Python instructions
    "/.well-known/appspecific/com.chrome.devtools", # Chrome devtools
]

logger = logging.getLogger("vault_system")
logger.setLevel(logging.INFO)

handler = RotatingFileHandler(
    SERVER_LOGS_FILE,
    maxBytes=10*1024*1024,  # 10MB per file
    backupCount=5,           # Keep 5 backup files
    encoding='utf-8'
)

formatter = logging.Formatter(
    '[%(asctime)s] %(message)s',
    datefmt='%Y-%m-%d %H:%M:%S'
)
handler.setFormatter(formatter)
logger.addHandler(handler)

# --- Uvicorn Server logs handling ---
class UvicornForwardHandler(logging.Handler):
    def emit(self, record):
        prefix = record.levelname  # INFO / WARNING / ERROR
        msg = record.getMessage()
        server_log(prefix, msg)

uvicorn_handler = UvicornForwardHandler()
uvicorn_handler.setFormatter(formatter)

for name in ["uvicorn", "uvicorn.error", "uvicorn.access"]:
    l = logging.getLogger(name)
    l.handlers = []          # Remove default handlers
    l.propagate = False
    l.addHandler(uvicorn_handler)
    l.setLevel(logging.INFO)

def server_log(prefix: str, text: str = None):
    message = sanitize_log_input(text)
    if message is None:
        # If it's a true "PREFIX: message"
        if re.match(r"^(INFO|WARNING|ERROR|DEBUG|CRITICAL|NOTICE|ADMIN|COMMAND|RISK)\b", prefix):
            if ":" in prefix:
                pfx, msg = prefix.split(":", 1)
                prefix = pfx.strip()
                message = msg.lstrip()
            else:
                message = prefix
                prefix = "INFO"
        else:
            # Access log → do NOT split by colon
            message = prefix
            prefix = "INFO"

    # Filter out ignored patterns
    if any(pattern in (message or "") for pattern in IGNORED_LOG_PATTERNS):
        return

    http_code = None
    status_text = None

    m = re.search(r"\b([1-5][0-9]{2})\b(?!.*\b[1-5][0-9]{2}\b)", message)
    if m:
        http_code = int(m.group(1))

        # Map codes to reason phrases
        http_reason_map = {
            1: "Error",
            100: "Continue",
            200: "OK",
            201: "Created",
            204: "No Content",
            301: "Moved Permanently",
            302: "Found",
            307: "Temporary Redirect",
            308: "Permanent Redirect",
            400: "Bad Request",
            401: "Unauthorized",
            403: "Forbidden",
            404: "Not Found",
            405: "Method Not Allowed",
            409: "Conflict",
            422: "Unprocessable Entity",
            429: "Too Many Requests",
            500: "Internal Server Error",
            502: "Bad Gateway",
            503: "Service Unavailable",
        }

        status_text = http_reason_map.get(http_code, "Unknown")
        message = message[:m.start()] + message[m.end():]
        message = message.strip()

    append_info = ""
    append_color = "\033[0m"
    if http_code:
        if 200 <= http_code < 300:
            append_info = f" {http_code} - {status_text}"
            append_color = "\033[32m"
        elif 300 <= http_code < 400:
            append_info = f" {http_code} - {status_text}"
            append_color = "\033[36m"
        elif 400 <= http_code < 500:
            append_info = f" {http_code} - {status_text}"
            append_color = "\033[33m"
        elif 500 <= http_code < 600:
            append_info = f" {http_code} - {status_text}"
            append_color = "\033[31m"

    pad_len = max(1, 9 - len(prefix))
    padding = " " * pad_len

    color = "\033[35m"

    if "INFO" in prefix:
        color = "\033[32m"
    if any(x in prefix for x in ["WARNING", "ENCRYPTED", "DEBUG", "RESETTING", "NOTICE"]):
        color = "\033[33m"
    if any(x in prefix for x in ["ADMIN", "COMMAND"]):
        color = "\033[36m"
    if any(x in prefix for x in ["RISK", "CRITICAL", "ERROR"]):
        color = "\033[31m"

    output = f"{color}{prefix}\033[0m:{padding}{message}{append_color}{append_info}\033[0m"

    logger.info(output)
    # print(f"{output}\n", end="")

def sanitize_log_input(text: str) -> str:
    """Remove ANSI escape sequences and control characters from log input."""
    # Remove ANSI escape sequences
    ansi_escape = re.compile(r'\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])')
    text = ansi_escape.sub('', text)
    # Remove other control characters except newline/tab
    text = ''.join(char for char in text if ord(char) >= 32 or char in '\n\t')
    # Replace newlines to prevent log injection
    text = text.replace('\n', '\\n').replace('\r', '\\r')
    return text

def debug_log(message: str):
    server_log("DEBUG", message)