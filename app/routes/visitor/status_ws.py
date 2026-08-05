# app/routes/ws.py
from fastapi import APIRouter, WebSocket, WebSocketDisconnect, Depends
from sqlalchemy.orm import Session
from app.database import get_db
from app.models_db import VisitSession, Employee
from app.services.connection_manager import manager
from app.services.visitor_status_service import build_visitor_status

router = APIRouter()


@router.websocket("/ws/status/{status_token}")
async def visitor_status_socket(websocket: WebSocket, status_token: str, db: Session = Depends(get_db)):
    session = db.query(VisitSession).filter(VisitSession.status_token == status_token).first()
    if session is None:
        await websocket.close(code=4404)  # custom close code, "not found"
        return

    await manager.connect(status_token, websocket)

    # Send current state immediately on connect — covers the case where the
    # host already responded before the visitor's page loaded.
    if session.host_response and session.selected_host_id:
        employee = db.query(Employee).filter(Employee.id == session.selected_host_id).first()
        status = build_visitor_status(
            response=session.host_response,
            employee=employee,
            wait_minutes=None,
            wait_until=session.wait_until,
            available_again_at=session.available_again_at,
        )
        await websocket.send_json({
            "state": status["visitor_state"],
            "visitor_message": status["visitor_message"],
            "host_response": session.host_response,
            "wait_until": session.wait_until.isoformat() if session.wait_until else None,
            "available_again_at": session.available_again_at.isoformat() if session.available_again_at else None,
            "visitor_choice": session.visitor_choice,
        })
    else:
        # host hasn't responded yet — just send raw session state
        await websocket.send_json({
            "state": session.state,
            "host_response": None,
            "visitor_choice": session.visitor_choice,
        })

    try:
        while True:
            await websocket.receive_text()
    except WebSocketDisconnect:
        manager.disconnect(status_token, websocket)