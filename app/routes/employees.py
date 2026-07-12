from fastapi import APIRouter, UploadFile, File, Form, Depends, HTTPException
from sqlalchemy.orm import Session
from app.database import get_db
from app.models import EmployeeCreateResponse
from app.services.employee_service import create_employee_record

router = APIRouter()


@router.post("/employees", response_model=EmployeeCreateResponse)
async def create_employee(
    name: str = Form(...),
    floor_room: str = Form(None),
    phone_number: str = Form(None),
    email: str = Form(None),
    photo: UploadFile = File(...),
    db: Session = Depends(get_db)
):
    photo_bytes = await photo.read()

    employee, error = create_employee_record(
        db=db,
        name=name,
        floor_room=floor_room,
        phone_number=phone_number,
        email=email,
        photo_bytes=photo_bytes,
        original_filename=photo.filename
    )

    if error:
        raise HTTPException(status_code=400, detail=error)

    return EmployeeCreateResponse(
    id=employee.id,
    name=employee.name,
    floor_room=employee.floor_room,
    phone_number=employee.phone_number,
    email=employee.email,
    photo_path=employee.photo_path,
    embedding_created=employee.face_embedding is not None
)