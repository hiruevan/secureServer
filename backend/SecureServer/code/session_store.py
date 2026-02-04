import time
import threading

_session_store = {}
_lock = threading.Lock()

SESSION_TTL = 3600  # seconds

def create_session(session_id: str, login_secret: bytes):
    with _lock:
        _session_store[session_id] = {
            "login_secret": login_secret,
            "exp": int(time.time()) + SESSION_TTL
        }

def get_session(session_id: str):
    with _lock:
        session = _session_store.get(session_id)
        if not session:
            return None

        if session["exp"] < time.time():
            del _session_store[session_id]
            return None

        return session

def destroy_session(session_id: str):
    with _lock:
        _session_store.pop(session_id, None)

def cleanup_expired():
    now = int(time.time())
    with _lock:
        expired = [sid for sid, s in _session_store.items() if s["exp"] < now]
        for sid in expired:
            del _session_store[sid]
