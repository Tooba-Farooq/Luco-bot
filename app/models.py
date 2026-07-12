from pydantic import BaseModel
from typing import Optional

class DetectionResponse(BaseModel):
    status: str  # "idle" | "detecting" | "known" | "unknown"
    face_forward: bool = False
    forward_duration: float = 0.0  # seconds accumulated facing forward
    visitor_name: Optional[str] = None  # only set if status == "known"
    confidence: Optional[float] = None  # recognition confidence score

return EmployeeCreateResponse(
    id=employee.id,
    name=employee.name,
    floor_room=employee.floor_room,
    phone_number=employee.phone_number,
    email=employee.email,
    photo_path=employee.photo_path,
    embedding_created=employee.face_embedding is not None
)