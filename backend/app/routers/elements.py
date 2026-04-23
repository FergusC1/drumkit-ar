# elements.py
# Handles saving and retrieving kit profiles and their elements.
# The save endpoint accepts a complete kit in a single request,
# creating both the profile record and all element records atomically.

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from app.database import get_db
from app.models import KitElement, KitProfile, ElementType
from pydantic import BaseModel
from typing import List
import uuid

router = APIRouter(prefix="/elements", tags=["elements"])

# --- Pydantic models ---
# These define the expected request/response JSON structure.
# FastAPI uses them for automatic validation and documentation.

class ElementCreate(BaseModel):
    element_type: ElementType
    label: str
    pos_x_cm: float
    pos_y_cm: float
    pos_z_cm: float
    angle_deg: float
    height_cm: float

class ElementResponse(BaseModel):
    id: uuid.UUID
    profile_id: uuid.UUID
    element_type: ElementType
    label: str
    pos_x_cm: float
    pos_y_cm: float
    pos_z_cm: float
    angle_deg: float
    height_cm: float

    class Config:
        # Allows Pydantic to read data from SQLAlchemy model attributes
        from_attributes = True

class KitSaveRequest(BaseModel):
    profile_name: str
    description: str = ""
    stage_width_cm: float = 0.0
    stage_depth_cm: float = 0.0
    owner_id: uuid.UUID
    elements: List[ElementCreate]


# POST /elements/save
# Saves a complete kit profile and all its elements in a single request.
# Uses db.flush() to write the profile and get its generated UUID before
# creating the elements, which need profile_id as a foreign key.
# Both writes are part of the same transaction - if element creation fails,
# the profile is also rolled back.
@router.post("/save", response_model=dict)
def save_kit(request: KitSaveRequest, db: Session = Depends(get_db)):
    profile = KitProfile(
        owner_id=request.owner_id,
        name=request.profile_name,
        description=request.description,
        stage_width_cm=request.stage_width_cm,
        stage_depth_cm=request.stage_depth_cm
    )
    db.add(profile)

    # flush() sends the INSERT to the database within the current transaction
    # without committing, making profile.id available for the element foreign keys
    db.flush()

    for elem in request.elements:
        db.add(KitElement(
            profile_id=profile.id,
            element_type=elem.element_type,
            label=elem.label,
            pos_x_cm=elem.pos_x_cm,
            pos_y_cm=elem.pos_y_cm,
            pos_z_cm=elem.pos_z_cm,
            angle_deg=elem.angle_deg,
            height_cm=elem.height_cm
        ))

    db.commit()
    db.refresh(profile)

    return {
        "success": True,
        "profile_id": str(profile.id),
        "elements_saved": len(request.elements)
    }


# GET /elements/profile/{profile_id}
# Returns all elements for a given profile as a flat list.
# Used by GuidedSetupManager in the Unity app to load a saved kit layout.
@router.get("/profile/{profile_id}", response_model=List[ElementResponse])
def get_elements(profile_id: uuid.UUID, db: Session = Depends(get_db)):
    elements = db.query(KitElement).filter(
        KitElement.profile_id == profile_id
    ).all()

    if not elements:
        raise HTTPException(status_code=404, detail="No elements found for this profile")

    return elements