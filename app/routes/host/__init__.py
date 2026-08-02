from fastapi import APIRouter
from . import pending, respond

router = APIRouter(prefix="/host", tags=["host"])
router.include_router(pending.router)
router.include_router(respond.router)