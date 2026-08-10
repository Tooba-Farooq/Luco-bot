from fastapi import APIRouter, Depends
from sqlalchemy.orm import Session
from pydantic import BaseModel
from app.database import get_db
from app.models_db import Employee
from app.dependencies import get_current_employee

router = APIRouter()


class FloorRoomUpdate(BaseModel):
    floor_room: str


@router.patch("/profile/floor-room")
def update_floor_room(
    payload: FloorRoomUpdate,
    current_employee: Employee = Depends(get_current_employee),
    db: Session = Depends(get_db),
):
    current_employee.floor_room = payload.floor_room.strip()
    db.commit()
    db.refresh(current_employee)

    return {"detail": "Floor/room updated", "floor_room": current_employee.floor_room}