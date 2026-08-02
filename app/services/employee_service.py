import os
import uuid
from sqlalchemy.orm import Session
from app.models_db import Employee
from app.services.auth_service import generate_invite_token, invite_expiry, generate_employee_code

PHOTOS_DIR = "employee_photos"
os.makedirs(PHOTOS_DIR, exist_ok=True)


def save_employee_photo(photo_bytes: bytes, original_filename: str) -> str:
    """Saves uploaded photo bytes to disk with a unique filename, returns the path."""
    file_extension = original_filename.split(".")[-1]
    unique_filename = f"{uuid.uuid4()}.{file_extension}"
    photo_path = os.path.join(PHOTOS_DIR, unique_filename).replace("\\", "/")

    with open(photo_path, "wb") as f:
        f.write(photo_bytes)

    return photo_path


def create_employee_record(db: Session, name: str, floor_room: str, phone_number: str, email: str, photo_bytes: bytes, original_filename: str):
    photo_path = save_employee_photo(photo_bytes, original_filename)

    new_employee = Employee(
        name=name,
        floor_room=floor_room,
        phone_number=phone_number,
        email=email,  # was missing before
        photo_path=photo_path,
        invite_token=generate_invite_token(),
        invite_expires_at=invite_expiry(),
        is_active=False,
    )
    db.add(new_employee)
    db.commit()
    db.refresh(new_employee)

    new_employee.employee_code = generate_employee_code(new_employee.id)
    db.commit()
    db.refresh(new_employee)

    return new_employee, None