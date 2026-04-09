from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from app.database import get_db
from app.models import KitProfile
from pydantic import BaseModel
from typing import Optional
import uuid

router = APIRouter(prefix="/profiles", tags=["profiles"])

class ProfileCreate(BaseModel):
    name: str
    description: Optional[str] = None
    stage_width_cm: Optional[float] = None
    stage_depth_cm: Optional[float] = None

class ProfileResponse(BaseModel):
    id: uuid.UUID
    owner_id: uuid.UUID
    name: str
    description: Optional[str]
    stage_width_cm: Optional[float]
    stage_depth_cm: Optional[float]

    class Config:
        from_attributes = True

@router.post("/{owner_id}", response_model=ProfileResponse)
def create_profile(owner_id: uuid.UUID, profile: ProfileCreate, db: Session = Depends(get_db)):
    db_profile = KitProfile(
        owner_id=owner_id,
        name=profile.name,
        description=profile.description,
        stage_width_cm=profile.stage_width_cm,
        stage_depth_cm=profile.stage_depth_cm
    )
    db.add(db_profile)
    db.commit()
    db.refresh(db_profile)
    return db_profile

@router.get("/{profile_id}", response_model=ProfileResponse)
def get_profile(profile_id: uuid.UUID, db: Session = Depends(get_db)):
    profile = db.query(KitProfile).filter(KitProfile.id == profile_id).first()
    if not profile:
        raise HTTPException(status_code=404, detail="Profile not found")
    return profile

@router.get("/user/{owner_id}", response_model=list[ProfileResponse])
def get_user_profiles(owner_id: uuid.UUID, db: Session = Depends(get_db)):
    profiles = db.query(KitProfile).filter(KitProfile.owner_id == owner_id).all()
    return profiles