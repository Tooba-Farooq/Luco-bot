from fastapi import APIRouter, Depends, HTTPException, Request
from fastapi.security import OAuth2PasswordRequestForm
from sqlalchemy.orm import Session
from datetime import datetime, timezone

from app.database import get_db
from app.models_db import Employee
from app.dependencies import get_current_employee
from app.models import (
    ActivateRequest,
    RefreshRequest,
    DeviceTokenRequest,
    TokenResponse,
)
from app.services.auth_service import (
    verify_password,
    hash_password,
    create_access_token,
    create_refresh_token,
    decode_token,
)

router = APIRouter(prefix="/auth", tags=["auth"])


@router.post("/login", response_model=TokenResponse)
def login(form_data: OAuth2PasswordRequestForm = Depends(), db: Session = Depends(get_db)):
    # form_data.username = employee_code (e.g. "EMP-07")
    employee = db.query(Employee).filter(Employee.employee_code == form_data.username).first()
    if not employee or not employee.password_hash or not verify_password(
        form_data.password, employee.password_hash
    ):
        raise HTTPException(status_code=401, detail="Incorrect employee ID or password")
    if not employee.is_active:
        raise HTTPException(status_code=403, detail="Account not activated")

    return TokenResponse(
        access_token=create_access_token(employee.id),
        refresh_token=create_refresh_token(employee.id),
    )

@router.post("/activate")
def activate(payload: ActivateRequest, db: Session = Depends(get_db)):
    employee = db.query(Employee).filter(Employee.invite_token == payload.invite_token).first()

    if not employee or not employee.invite_expires_at:
        raise HTTPException(status_code=400, detail="Invalid or expired invite")

    invite_expires_at = employee.invite_expires_at
    if invite_expires_at.tzinfo is None:
        invite_expires_at = invite_expires_at.replace(tzinfo=timezone.utc)

    if invite_expires_at < datetime.now(timezone.utc):
        raise HTTPException(status_code=400, detail="Invalid or expired invite")

    employee.password_hash = hash_password(payload.password)
    employee.is_active = True
    employee.invite_token = None
    employee.invite_expires_at = None
    db.commit()
    return {"detail": "Password set. You can now log in.", "employee_code": employee.employee_code}


@router.post("/refresh", response_model=TokenResponse)
def refresh(payload: RefreshRequest):
    try:
        data = decode_token(payload.refresh_token)
        if data.get("type") != "refresh":
            raise ValueError()
    except ValueError:
        raise HTTPException(status_code=401, detail="Invalid refresh token")

    employee_id = int(data["sub"])
    return TokenResponse(
        access_token=create_access_token(employee_id),
        refresh_token=payload.refresh_token,  # unchanged; only access token rotates
    )


@router.post("/register-device")
def register_device(
    payload: DeviceTokenRequest,
    current_employee: Employee = Depends(get_current_employee),
    db: Session = Depends(get_db),
):
    current_employee.device_token = payload.device_token
    current_employee.device_platform = payload.platform
    db.commit()
    return {"detail": "Device registered"}

@router.get("/me/device-status")  # TEMP — remove after push testing is confirmed working
def device_status(current_employee: Employee = Depends(get_current_employee)):
    return {
        "device_registered": current_employee.device_token,
        "platform": current_employee.device_platform,
    }


@router.get("/me")
def me(request: Request, current_employee: Employee = Depends(get_current_employee)):
    photo_url = None
    if current_employee.photo_path:
        base_url = str(request.base_url).rstrip("/")
        photo_url = f"{base_url}/{current_employee.photo_path}"

    return {
        "id": current_employee.id,
        "employee_code": current_employee.employee_code,
        "name": current_employee.name,
        "photo_url": photo_url,
        "floor_room": current_employee.floor_room,
    }