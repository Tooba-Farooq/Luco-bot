from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from app.database import get_db
from app.services.detection_state import detection_state
from app.models_db import Employee
from app.models import ConfirmHostResponse, SelectHostRequest, RetryHostNameRequest

router = APIRouter()


@router.post("/session/confirm-host", response_model=ConfirmHostResponse)
async def confirm_host(payload: SelectHostRequest, db: Session = Depends(get_db)):
    if detection_state.session_id != payload.session_id:
        raise HTTPException(status_code=400, detail="Session mismatch")

    employee = db.query(Employee).filter(Employee.id == payload.employee_id).first()
    if not employee:
        raise HTTPException(status_code=404, detail="Employee not found")

    detection_state.selected_host_id = employee.id
    detection_state.host_candidates = None
    detection_state.state = "AWAITING_PURPOSE"

    return ConfirmHostResponse(
        session_id=payload.session_id,
        state="AWAITING_PURPOSE",
        matched_host={"id": employee.id, "name": employee.name},
        answer_text="Please tell me the purpose of your meeting?",
        audio_key="ask_purpose",
    )


@router.post("/session/cancel-host-selection")
async def cancel_host_selection(payload: RetryHostNameRequest):
    if detection_state.session_id != payload.session_id:
        raise HTTPException(status_code=400, detail="Session mismatch")
    raise HTTPException(status_code=501, detail="Not implemented yet")
