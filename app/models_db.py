from sqlalchemy import Column, Integer, String, DateTime, JSON, ForeignKey, Text, Boolean
from sqlalchemy.sql import func
from sqlalchemy.orm import relationship
from datetime import datetime, timezone
from app.database import Base

class Employee(Base):
    __tablename__ = "employees"
    id = Column(Integer, primary_key=True)
    name = Column(String, nullable=False)
    floor_room = Column(String, nullable=True)
    phone_number = Column(String, nullable=True)
    email = Column(String, nullable=True)
    photo_path = Column(String, nullable=True)
    created_at = Column(DateTime, default=lambda: datetime.now(timezone.utc))

    employee_code = Column(String, unique=True, nullable=True)
    password_hash = Column(String, nullable=True)
    is_active = Column(Boolean, default=False, nullable=False)
    invite_token = Column(String, unique=True, nullable=True)
    invite_expires_at = Column(DateTime, nullable=True)
    device_token = Column(String, nullable=True)
    device_platform = Column(String, nullable=True)

    visits = relationship("VisitLog", back_populates="host_employee")


class Visitor(Base):
    __tablename__ = "visitors"
    id = Column(Integer, primary_key=True)
    name = Column(String, nullable=True)
    face_embedding = Column(JSON, nullable=True)
    photo_path = Column(String, nullable=True)
    created_at = Column(DateTime, default=lambda: datetime.now(timezone.utc))

    visits = relationship("VisitLog", back_populates="visitor")

    


class VisitLog(Base):
    __tablename__ = "visit_logs"
    id = Column(Integer, primary_key=True)
    visitor_id = Column(Integer, ForeignKey("visitors.id"), nullable=False)
    host_employee_id = Column(Integer, ForeignKey("employees.id"), nullable=True)
    purpose = Column(String, nullable=True)
    status = Column(String, nullable=False, default="pending")
    message_text = Column(Text, nullable=True)
    created_at = Column(DateTime, default=lambda: datetime.now(timezone.utc))

    visitor = relationship("Visitor", back_populates="visits")
    host_employee = relationship("Employee", back_populates="visits")


class VisitSession(Base):  # your existing declarative Base
    __tablename__ = "visit_sessions"

    session_id = Column(String, primary_key=True)
    state = Column(String, nullable=False)

    visitor_id = Column(Integer, nullable=True)
    visit_log_id = Column(Integer, nullable=True)
    selected_host_id = Column(Integer, nullable=True)
    purpose = Column(String, nullable=True)
    recognized_name = Column(String, nullable=True)

    is_closed = Column(Boolean, default=False, nullable=False)
    created_at = Column(DateTime, server_default=func.now(), nullable=False)
    last_active_at = Column(DateTime, server_default=func.now(), onupdate=func.now(), nullable=False)

    # --- host-alert flow ---
    host_alert_sent_at = Column(DateTime, nullable=True)
    host_response = Column(String, nullable=True)       # "available" | "not_available" | "wait" | null
    visitor_choice = Column(String, nullable=True)       # "wait" | "message" | "cancel" | null
    message_text = Column(Text, nullable=True)
    status_token = Column(String, unique=True, nullable=True)  # public token for visitor's status page/socket
