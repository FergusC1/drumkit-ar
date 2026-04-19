from sqlalchemy import Column, String, Float, DateTime, Enum, ForeignKey
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import relationship
from app.database import Base
import uuid
import datetime
import enum

class UserRole(enum.Enum):
    drummer = "drummer"
    sound_engineer = "sound_engineer"
    promoter = "promoter"
    backline = "backline"
    support_act = "support_act"

class User(Base):
    __tablename__ = "users"

    id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    username = Column(String, unique=True, nullable=False)
    email = Column(String, unique=True, nullable=False)
    role = Column(Enum(UserRole), nullable=False)
    created_at = Column(DateTime, default=datetime.datetime.utcnow)

    profiles = relationship("KitProfile", back_populates="owner")

class KitProfile(Base):
    __tablename__ = "kit_profiles"

    id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    owner_id = Column(UUID(as_uuid=True), ForeignKey("users.id"), nullable=False)
    name = Column(String, nullable=False)
    description = Column(String)
    stage_width_cm = Column(Float)
    stage_depth_cm = Column(Float)
    created_at = Column(DateTime, default=datetime.datetime.utcnow)

    owner = relationship("User", back_populates="profiles")
    elements = relationship("KitElement", back_populates="profile")

class ElementType(enum.Enum):
    kick_drum = "kick_drum"
    snare_drum = "snare_drum"
    hi_hat = "hi_hat"
    ride_cymbal = "ride_cymbal"
    crash_cymbal = "crash_cymbal"
    floor_tom = "floor_tom"
    rack_tom = "rack_tom"
    splash = "splash"
    china = "china"
    drum_throne = "drum_throne"

class KitElement(Base):
    __tablename__ = "kit_elements"

    id = Column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    profile_id = Column(UUID(as_uuid=True), ForeignKey("kit_profiles.id"), nullable=False)
    element_type = Column(Enum(ElementType), nullable=False)
    label = Column(String, nullable=False)
    pos_x_cm = Column(Float, nullable=False)
    pos_y_cm = Column(Float, nullable=False)
    pos_z_cm = Column(Float, nullable=False)
    angle_deg = Column(Float, nullable=False)
    height_cm = Column(Float, nullable=False)

    profile = relationship("KitProfile", back_populates="elements")