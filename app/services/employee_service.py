import os
import uuid
from sqlalchemy.orm import Session
from app.models_db import Employee
from app.services.embedding_service import generate_face_embedding

PHOTOS_DIR = "employee_photos"
os.makedirs(PHOTOS_DIR, exist_ok=True)


def save_employee_photo(photo_bytes: bytes, original_filename: str) -> str:
    """Saves uploaded photo bytes to disk with a unique filename, returns the path."""
    file_extension = original_filename.split(".")[-1]
    unique_filename = f"{uuid.uuid4()}.{file_extension}"
    photo_path = os.path.join(PHOTOS_DIR, unique_filename)

    with open(photo_path, "wb") as f:
        f.write(photo_bytes)

    return photo_path


def create_employee_record(db: Session, name: str, floor_room: str, phone_number: str, email: str, photo_bytes: bytes, original_filename: str):
    photo_path = save_employee_photo(photo_bytes, original_filename)

    embedding = generate_face_embedding(photo_path)
    embedding_created = embedding is not None
    # photo and record are still saved either way — face recognition is a
    # bonus capability, not a requirement for being a valid employee record

    new_employee = Employee(
        name=name,
        floor_room=floor_room,
        phone_number=phone_number,
        photo_path=photo_path,
        face_embedding=embedding  # None if detection failed — handled fine everywhere else
    )
    db.add(new_employee)
    db.commit()
    db.refresh(new_employee)

    return new_employee, None  # no error — registration succeeds regardless