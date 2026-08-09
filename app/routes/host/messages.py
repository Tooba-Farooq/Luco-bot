# app/routes/host/messages.py
from fastapi import APIRouter, Depends
from sqlalchemy.orm import Session
from app.database import get_db
from app.models_db import Employee, VisitSession, VisitLog, Visitor
from app.dependencies import get_current_employee
import os

router = APIRouter()
PUBLIC_BASE_URL = os.getenv("PUBLIC_BASE_URL", "http://127.0.0.1:8000")


@router.get("/messages")
def get_messages(
    current_employee: Employee = Depends(get_current_employee),
    db: Session = Depends(get_db),
):
    logs = (
        db.query(VisitLog)
        .filter(
            VisitLog.host_employee_id == current_employee.id,
            VisitLog.message_text.isnot(None),
        )
        .order_by(VisitLog.created_at.desc())
        .all()
    )

    results = []
    for log in logs:
        # each VisitLog is pointed to by exactly one VisitSession (set at
        # persist_at_handoff) — look it up to get session_id for the app's
        # visit-specific actions/traceability
        session = (
            db.query(VisitSession)
            .filter(VisitSession.visit_log_id == log.id)
            .first()
        )

        visitor = db.query(Visitor).filter(Visitor.id == log.visitor_id).first()
        visitor_name = (visitor.name if visitor else None) or "A visitor"
        visitor_photo_url = ""
        if visitor and visitor.photo_path:
            visitor_photo_url = f"{PUBLIC_BASE_URL}/{visitor.photo_path}"

        results.append({
            "session_id": session.session_id if session else None,
            "visitor_id": log.visitor_id,
            "visitor_name": visitor_name,
            "visitor_photo_url": visitor_photo_url,
            "message_text": log.message_text,
            "purpose": log.purpose or "",
            "left_at": log.created_at.isoformat() if log.created_at else None,
        })

    return {"messages": results}