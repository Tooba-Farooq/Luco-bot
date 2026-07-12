from sqlalchemy import Column, Integer, String, DateTime, JSON, ForeignKey, Text
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
    face_embedding = Column(JSON, nullable=True)
    created_at = Column(DateTime, default=lambda: datetime.now(timezone.utc))

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