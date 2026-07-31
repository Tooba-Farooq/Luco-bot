from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from datetime import datetime, timezone
from pydantic import BaseModel

from app.database import get_db
from app.models_db import Employee, VisitSession
from app.models import HostRespondRequest
from app.dependencies import get_current_employee
from app.services.connection_manager import manager

router = APIRouter(prefix="/session", tags=["host-response"])


@router.post("/host-respond")
async def host_respond(
    payload: HostRespondRequest,
    current_employee: Employee = Depends(get_current_employee),
    db: Session = Depends(get_db),
):
    session = db.query(VisitSession).filter(VisitSession.session_id == payload.session_id).first()
    if session is None:
        raise HTTPException(status_code=404, detail="Session not found")

    if session.selected_host_id != current_employee.id:
        raise HTTPException(status_code=403, detail="This session is not assigned to you")

    if payload.response not in ("available", "not_available", "wait"):
        raise HTTPException(status_code=400, detail="Invalid response value")

    session.host_response = payload.response
    db.commit()

    if session.status_token:
        await manager.send_update(session.status_token, {
            "state": session.state,
            "host_response": session.host_response,
            "visitor_choice": session.visitor_choice,
        })

    return {"detail": "Response recorded", "host_response": session.host_response}