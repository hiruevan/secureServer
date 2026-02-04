import os
from fastapi import Response
from starlette.middleware.base import BaseHTTPMiddleware
from fastapi.staticfiles import StaticFiles
from SecureServer.code.environment_variables import USE_HTTPS

class SecurityHeadersMiddleware(BaseHTTPMiddleware):
    async def dispatch(self, request, call_next):
        response = await call_next(request)
        
        response.headers["Content-Security-Policy"] = (
            "default-src 'self'; "
            "script-src 'self'; "
            "style-src 'self' 'unsafe-inline'; " 
            "frame-ancestors 'none';"
        )
        
        response.headers["X-Frame-Options"] = "DENY"
        response.headers["X-Content-Type-Options"] = "nosniff"
        response.headers["Referrer-Policy"] = "strict-origin-when-cross-origin"
        
        response.headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload" 
        
        return response
    
class HTTPSRedirectMiddleware(BaseHTTPMiddleware):
    async def dispatch(self, request, call_next):
        if request.url.scheme != "https" and USE_HTTPS:
            url = request.url.replace(scheme="https")
            return Response(status_code=301, headers={"Location": str(url)})
        return await call_next(request)
    
class StaticFilesWithHeaders(StaticFiles):
    """StaticFiles with security headers."""
    async def get_response(self, path: str, scope):
        response = await super().get_response(path, scope)
        # Apply security headers
        response.headers["Content-Security-Policy"] = (
            "default-src 'self'; "
            "script-src 'self'; "
            "style-src 'self' 'unsafe-inline'; "
            "frame-ancestors 'none';"
        )
        response.headers["X-Frame-Options"] = "DENY"
        response.headers["X-Content-Type-Options"] = "nosniff"
        return response