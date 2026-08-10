from fastapi import APIRouter
from . import pending, respond, messages, history, profile

router = APIRouter(prefix="/host", tags=["host"])
router.include_router(pending.router)
router.include_router(respond.router)
router.include_router(messages.router)
router.include_router(history.router)
router.include_router(profile.router)

# respond.py
  # no prefix, no tags — __init__.py owns both
