from pydantic import BaseModel
from typing import List, Optional


class HostSummary(BaseModel):
    id: int
    name: str

class DetectionResponse(BaseModel):
    status: str
    face_forward: bool = False
    forward_duration: float = 0.0
    visitor_name: Optional[str] = None
    confidence: Optional[float] = None
    audio_base64: Optional[str] = None  # dynamic audio (known-visitor greeting)
    audio_key: Optional[str] = None     # static audio (fetch via GET /audio/{key})
    session_id: Optional[str] = None
    answer_text: Optional[str] = None

class EmployeeCreateResponse(BaseModel):
    id: int
    name: str
    floor_room: Optional[str] = None
    phone_number: Optional[str] = None
    email: Optional[str] = None
    photo_path: Optional[str] = None
    embedding_created: bool

class RespondResponse(BaseModel):
    session_id: str
    state: str
    heard_text: str
    detected_lang: str
    answer_text: Optional[str] = None
    matched_host: Optional[HostSummary] = None
    host_candidates: Optional[List[HostSummary]] = None
    audio_base64: Optional[str] = None       # ← add if missing
    audio_key: Optional[str] = None 

class PhotoFrameResponse(BaseModel):
    face_found: bool
    is_forward: bool
    is_centered: bool
    ready_to_capture: bool


class SelectHostRequest(BaseModel):
    session_id: str
    employee_id: int


class RetryHostNameRequest(BaseModel):
    session_id: str


class SubmitNameRequest(BaseModel):
    session_id: str
    name: str


class ConfirmHostResponse(BaseModel):
    session_id: str
    state: str
    matched_host: HostSummary
    answer_text: str
    audio_key: str


class SubmitNameResponse(BaseModel):
    session_id: str
    state: str
    visitor_name: str
    answer_text: str
    audio_key: str

    