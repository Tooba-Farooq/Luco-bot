from dotenv import load_dotenv
from fastapi.staticfiles import StaticFiles

load_dotenv()


from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from app.routes import detection, employees, audio, session, auth, host, visitor
from app.routes.visitor import status_ws
from app.database import engine, Base
from contextlib import asynccontextmanager
from app.services.tts_service import build_static_audio_cache
from app import models_db  # ensures models are registered before create_all runs
from fastapi.staticfiles import StaticFiles
from app.services.wait_reminder_scheduler import start_scheduler, stop_scheduler
from app.services.wait_reminder_scheduler import start_scheduler, stop_scheduler, reconcile_pending_wait_reminders


# Base.metadata.create_all(bind=engine)


@asynccontextmanager
async def lifespan(app: FastAPI):
    await build_static_audio_cache()
    start_scheduler()
    await reconcile_pending_wait_reminders()
    yield
    stop_scheduler()

app = FastAPI(lifespan=lifespan)


app = FastAPI(title="Reception Robot Backend", lifespan=lifespan)

app.add_middleware(
    CORSMiddleware,
    allow_origins=[
        "https://luco-bot-activation.netlify.app",
        "https://lucobot-visit-status.netlify.app",
        "http://localhost:5500",
        "http://127.0.0.1:5500",
    ],
    allow_methods=["*"],
    allow_headers=["*"],
)


app.mount("/employee_photos", StaticFiles(directory="employee_photos"), name="employee_photos")
app.mount("/visitor_photos", StaticFiles(directory="visitor_photos"), name="visitor_photos")

app.include_router(detection.router)
app.include_router(employees.router)
app.include_router(audio.router)
app.include_router(session.router)
app.include_router(auth.router)
app.include_router(host.router)
app.include_router(visitor.router)
app.include_router(status_ws.router)