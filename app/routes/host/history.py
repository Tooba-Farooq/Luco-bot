# app/routes/host/history.py
from fastapi import APIRouter, Depends, Query
from sqlalchemy.orm import Session
from app.database import get_db
from app.models_db import Employee, VisitSession, Visitor
from app.dependencies import get_current_employee
import os

router = APIRouter()
PUBLIC_BASE_URL = os.getenv("PUBLIC_BASE_URL", "http://127.0.0.1:8000")


@router.get("/alert-history")
def get_alert_history(
    current_employee: Employee = Depends(get_current_employee),
    db: Session = Depends(get_db),
    limit: int = Query(20, ge=1, le=100, description="Max number of results to return"),
    offset: int = Query(0, ge=0, description="Number of results to skip, for pagination"),
):
    base_query = db.query(VisitSession).filter(
        VisitSession.selected_host_id == current_employee.id,
        VisitSession.host_response.in_(["available", "not_available"]),
    )

    total = base_query.count()

    sessions = (
        base_query
        .order_by(VisitSession.host_alert_sent_at.desc())
        .offset(offset)
        .limit(limit)
        .all()
    )

    results = []
    for session in sessions:
        visitor_name = session.recognized_name or "A visitor"
        visitor_photo_url = ""
        if session.visitor_id:
            visitor = db.query(Visitor).filter(Visitor.id == session.visitor_id).first()
            if visitor and visitor.photo_path:
                visitor_photo_url = f"{PUBLIC_BASE_URL}/{visitor.photo_path}"

        results.append({
            "session_id": session.session_id,
            "visitor_id": session.visitor_id,
            "visitor_name": visitor_name,
            "visitor_photo_url": visitor_photo_url,
            "purpose": session.purpose or "",
            "arrived_at": session.host_alert_sent_at.isoformat() if session.host_alert_sent_at else None,
            "host_response": session.host_response,
            "available_again_at": session.available_again_at.isoformat() if session.available_again_at else None,
        })

    return {
        "history": results,
        "total": total,
        "limit": limit,
        "offset": offset,
        "has_more": offset + len(results) < total,
    }