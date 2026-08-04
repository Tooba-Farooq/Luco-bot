# app/routes/host/messages.py
from fastapi import APIRouter, Depends
from sqlalchemy.orm import Session
from app.database import get_db
from app.models_db import Employee, VisitSession, Visitor
from app.dependencies import get_current_employee
import os

router = APIRouter()
PUBLIC_BASE_URL = os.getenv("PUBLIC_BASE_URL", "http://127.0.0.1:8000")


@router.get("/messages")
def get_messages(
    current_employee: Employee = Depends(get_current_employee),
    db: Session = Depends(get_db),
):
    sessions = (
        db.query(VisitSession)
        .filter(
            VisitSession.selected_host_id == current_employee.id,
            VisitSession.message_text.isnot(None),
        )
        .order_by(VisitSession.host_alert_sent_at.desc())
        .all()
    )

    results = []
    for session in sessions:
        visitor_name = session.recognized_name or "A visitor"
        visitor_id = session.visitor_id
        visitor_photo_url = ""
        if visitor_id:
            visitor = db.query(Visitor).filter(Visitor.id == visitor_id).first()
            if visitor and visitor.photo_path:
                visitor_photo_url = f"{PUBLIC_BASE_URL}/{visitor.photo_path}"

        results.append({
            "session_id": session.session_id,
            "visitor_id": visitor_id,
            "visitor_name": visitor_name,
            "visitor_photo_url": visitor_photo_url,
            "message_text": session.message_text,
            "purpose": session.purpose or "",
            "left_at": session.host_alert_sent_at.isoformat() if session.host_alert_sent_at else None,
        })

    return {"messages": results}