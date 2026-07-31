from fastapi import WebSocket
from typing import Dict


class ConnectionManager:
    def __init__(self):
        self._connections: Dict[str, WebSocket] = {}

    async def connect(self, status_token: str, websocket: WebSocket):
        await websocket.accept()
        self._connections[status_token] = websocket

    def disconnect(self, status_token: str):
        self._connections.pop(status_token, None)

    async def send_update(self, status_token: str, payload: dict):
        websocket = self._connections.get(status_token)
        if websocket is None:
            return False  # visitor's page isn't connected (closed tab, not yet opened, etc.) — not an error
        try:
            await websocket.send_json(payload)
            return True
        except Exception:
            self.disconnect(status_token)
            return False


manager = ConnectionManager()