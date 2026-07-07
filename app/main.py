from fastapi import FastAPI
from app.routes import detection

app = FastAPI(title="Lucobot Backend")
app.include_router(detection.router)