from fastapi import FastAPI
from app.routes import detection, employees
from app.database import engine, Base
from app import models_db  # ensures models are registered before create_all runs

Base.metadata.create_all(bind=engine)

app = FastAPI(title="Reception Robot Backend")
app.include_router(detection.router)
app.include_router(employees.router)