from dotenv import load_dotenv
load_dotenv()


from fastapi import FastAPI
from app.routes import detection, employees, audio, session
from app.database import engine, Base
from contextlib import asynccontextmanager
from app.services.tts_service import build_static_audio_cache
from app import models_db  # ensures models are registered before create_all runs


Base.metadata.create_all(bind=engine)

@asynccontextmanager
async def lifespan(app: FastAPI):
    # startup
    await build_static_audio_cache()
    yield
    # shutdown (nothing to do yet, but this is where cleanup would go)


app = FastAPI(title="Reception Robot Backend", lifespan=lifespan)
app.include_router(detection.router)
app.include_router(employees.router)
app.include_router(audio.router)
app.include_router(session.router)