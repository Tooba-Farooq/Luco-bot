from fastapi import WebSocket
from typing import Dict


class ConnectionManager:
    def __init__(self):
        self._connections: Dict[str, WebSocket] = {}

    async def connect(self, status_token: str, websocket: WebSocket):
        await websocket.accept()
        self._connections[status_token] = websocket

    def disconnect(self, status_token: str, websocket: WebSocket = None):
        # only remove if this is still the currently-registered socket for the token —
        # prevents a stale/delayed disconnect from a superseded connection wiping out
        # a newer, live one
        if websocket is None or self._connections.get(status_token) is websocket:
            self._connections.pop(status_token, None)

    async def send_update(self, status_token: str, payload: dict):
        websocket = self._connections.get(status_token)
        if websocket is None:
            return False
        try:
            await websocket.send_json(payload)
            return True
        except Exception:
            self.disconnect(status_token, websocket)
            return False

    async def expire(self, status_token: str):
        websocket = self._connections.pop(status_token, None)
        if websocket is not None:
            try:
                await websocket.close()
            except Exception:
                pass


manager = ConnectionManager()