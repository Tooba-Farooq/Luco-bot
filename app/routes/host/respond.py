from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from datetime import datetime, timedelta, timezone
from zoneinfo import ZoneInfo

from app.database import get_db
from app.models_db import Employee, VisitSession, VisitLog
from app.models import HostRespondRequest
from app.dependencies import get_current_employee
from app.services.connection_manager import manager
from app.services.visitor_status_service import build_visitor_status

router = APIRouter()

VALID_RESPONSES = ("available", "not_available", "wait")
LOCAL_TZ = ZoneInfo("Asia/Karachi")


from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from datetime import datetime, timedelta, timezone
from zoneinfo import ZoneInfo

from app.database import get_db
from app.models_db import Employee, VisitSession, VisitLog
from app.models import HostRespondRequest
from app.dependencies import get_current_employee
from app.services.connection_manager import manager
from app.services.visitor_status_service import build_visitor_status

router = APIRouter()

VALID_RESPONSES = ("available", "not_available", "wait")
LOCAL_TZ = ZoneInfo("Asia/Karachi")


@router.post("/respond")
async def host_respond(
    payload: HostRespondRequest,
    current_employee: Employee = Depends(get_current_employee),
    db: Session = Depends(get_db),
):
    session = db.query(VisitSession).filter(
        VisitSession.session_id == payload.session_id
    ).first()
    if session is None:
        raise HTTPException(status_code=404, detail="Session not found")

    if session.selected_host_id != current_employee.id:
        raise HTTPException(status_code=403, detail="This session is not assigned to you")

    if payload.response not in VALID_RESPONSES:
        raise HTTPException(status_code=400, detail="Invalid response value")

    from app.services.wait_reminder_scheduler import schedule_wait_reminder, cancel_wait_reminder

    if payload.response == "wait":
        if not payload.wait_minutes or payload.wait_minutes <= 0:
            raise HTTPException(status_code=400, detail="wait_minutes is required and must be positive when response is 'wait'")
        session.wait_until = datetime.now(timezone.utc) + timedelta(minutes=payload.wait_minutes)
        session.wait_minutes = payload.wait_minutes
        session.wait_reminder_sent_at = None
        session.available_again_at = None
        schedule_wait_reminder(session.session_id, session.wait_until)

    elif payload.response == "not_available":
        session.wait_until = None
        session.wait_minutes = None
        session.wait_reminder_sent_at = None
        cancel_wait_reminder(session.session_id)
        if payload.available_again_at:
            local_dt = payload.available_again_at.replace(tzinfo=LOCAL_TZ)
            session.available_again_at = local_dt.astimezone(timezone.utc)
        else:
            session.available_again_at = None

    elif payload.response == "available":
        session.wait_until = None
        session.wait_minutes = None
        session.wait_reminder_sent_at = None
        cancel_wait_reminder(session.session_id)

    session.host_response = payload.response
    db.commit()

    # --- visit logging ---
    if payload.response in ("available", "not_available"):
        visit_log = db.query(VisitLog).filter(VisitLog.id == session.visit_log_id).first()
        if visit_log:
            visit_log.status = "completed" if payload.response == "available" else "host_unavailable"
            db.commit()

    status = build_visitor_status(
        response=payload.response,
        employee=current_employee,
        wait_minutes=payload.wait_minutes,
        wait_until=session.wait_until,
        available_again_at=session.available_again_at,
    )

    if session.status_token:
        await manager.send_update(session.status_token, {
            "state": status["visitor_state"],
            "visitor_message": status["visitor_message"],
            "host_response": session.host_response,
            "wait_until": session.wait_until.isoformat() if session.wait_until else None,
            "available_again_at": session.available_again_at.isoformat() if session.available_again_at else None,
            "visitor_choice": session.visitor_choice,
        })

    return {
        "detail": "Response recorded",
        "host_response": session.host_response,
        "wait_until": session.wait_until,
        "available_again_at": session.available_again_at,
        **status,
    }