from fastapi import APIRouter, WebSocket, WebSocketDisconnect, Depends
from sqlalchemy.orm import Session
from app.database import get_db
from app.models_db import VisitSession
from app.services.connection_manager import manager

router = APIRouter()


@router.websocket("/ws/status/{status_token}")
async def visitor_status_socket(websocket: WebSocket, status_token: str, db: Session = Depends(get_db)):
    session = db.query(VisitSession).filter(VisitSession.status_token == status_token).first()
    if session is None:
        await websocket.close(code=4404)  # custom close code, "not found"
        return

    await manager.connect(status_token, websocket)

    # Send current state immediately on connect — don't make the visitor wait for the next event
    await websocket.send_json({
        "state": session.state,
        "host_response": session.host_response,
        "visitor_choice": session.visitor_choice,
    })

    try:
        while True:
            # Visitor's page doesn't need to send anything, but we still need to
            # await something to detect disconnect — this just waits and discards.
            await websocket.receive_text()
    except WebSocketDisconnect:
        manager.disconnect(status_token)