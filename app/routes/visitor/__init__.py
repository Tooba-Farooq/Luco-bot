from fastapi import APIRouter
from . import message, status_ws  # status_ws = your moved ws.py content

router = APIRouter(prefix="/visitor", tags=["visitor"])
router.include_router(message.router)
