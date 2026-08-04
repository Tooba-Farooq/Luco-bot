from fastapi import APIRouter
from . import respond, host, name, photo

router = APIRouter()
router.include_router(respond.router)
router.include_router(host.router)
router.include_router(name.router)
router.include_router(photo.router)
