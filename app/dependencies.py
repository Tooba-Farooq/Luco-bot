from fastapi import Depends, HTTPException, status
from fastapi.security import OAuth2PasswordBearer
from sqlalchemy.orm import Session
from app.database import get_db
from app.models_db import Employee
from app.services.auth_service import decode_token

oauth2_scheme = OAuth2PasswordBearer(tokenUrl="/auth/login", scheme_name="EmployeeAuth")
admin_oauth2_scheme = OAuth2PasswordBearer(tokenUrl="/auth/admin-login", scheme_name="AdminAuth")

def get_current_employee(
    token: str = Depends(oauth2_scheme),
    db: Session = Depends(get_db),
) -> Employee:
    try:
        payload = decode_token(token)
        if payload.get("type") != "access":
            raise ValueError("Wrong token type")
        employee_id = int(payload["sub"])
    except (ValueError, KeyError):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED, detail="Token expired or invalid — please log in again"
        )

    employee = db.query(Employee).filter(Employee.id == employee_id).first()
    if not employee or not employee.is_active:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Employee not found or inactive",
        )
    return employee

def get_current_admin(token: str = Depends(admin_oauth2_scheme)) -> bool:
    try:
        payload = decode_token(token)
        if payload.get("type") != "admin":
            raise ValueError("Wrong token type")
    except ValueError:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Token expired or invalid — please log in again",
        )
    return True