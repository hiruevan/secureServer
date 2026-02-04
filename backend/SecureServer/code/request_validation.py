import re, string
from pydantic import BaseModel, ConfigDict, Field, field_validator
from typing import Optional

class SignupRequest(BaseModel):
    username: str = Field(..., min_length=3, max_length=32)
    password: str = Field(..., min_length=12, max_length=72)

    first_name: Optional[str] = None
    last_name: Optional[str] = None
    email: Optional[str] = None
    phone: Optional[str] = None
    preferred_contact_method: Optional[str] = None

    model_config = ConfigDict(extra="ignore")
    
    @field_validator('username')
    def username_alphanumeric(cls, v):
        if not re.match(r'^[a-zA-Z0-9_]+$', v) or any(c in string.punctuation for c in v):
            raise ValueError('Username must be alphanumeric')
        return v
    
    @field_validator('password')
    def password_strength(cls, v):
        if not any(c.isupper() for c in v):
            raise ValueError('Password must contain uppercase letter')
        if not any(c.islower() for c in v):
            raise ValueError('Password must contain lowercase letter')
        if not any(c.isdigit() for c in v):
            raise ValueError('Password must contain number')
        if not any(c in string.punctuation for c in v):
            raise ValueError('Password must contain special character')
        return v
class VaultUpdateRequest(BaseModel):
    data: str = Field(..., max_length=100000)  # 100KB limit
class LoginRequest(BaseModel):
    username: str = Field(..., min_length=1, max_length=32)
    password: str = Field(..., min_length=1, max_length=72)
    totp_code: Optional[str] = None
class PasswordChangeRequest(BaseModel):
    old_password: str = Field(..., min_length=1, max_length=72)
    new_password: str = Field(..., min_length=12, max_length=72)
    
    @field_validator('new_password')
    def password_strength(cls, v):
        if not any(c.isupper() for c in v):
            raise ValueError('Password must contain uppercase letter')
        if not any(c.islower() for c in v):
            raise ValueError('Password must contain lowercase letter')
        if not any(c.isdigit() for c in v):
            raise ValueError('Password must contain number')
        if not any(c in string.punctuation for c in v):
            raise ValueError('Password must contain special character')
        return v