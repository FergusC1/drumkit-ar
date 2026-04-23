from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from app.database import get_db
from app.models import KitProfile, KitElement
from pydantic import BaseModel
from typing import Optional, List
import uuid
import datetime
import secrets
import enum

router = APIRouter(prefix="/shares", tags=["shares"])

class ViewLevel(str, enum.Enum):
    full = "full"
    technical = "technical"
    footprint = "footprint"
    inventory = "inventory"

class ShareLinkCreate(BaseModel):
    profile_id: uuid.UUID
    view_level: ViewLevel
    expires_hours: int = 24

class ShareLinkResponse(BaseModel):
    token: str
    view_level: ViewLevel
    expires_at: datetime.datetime
    share_url: str

class FootprintView(BaseModel):
    profile_name: str
    stage_width_cm: Optional[float]
    stage_depth_cm: Optional[float]
    element_count: int

class InventoryItem(BaseModel):
    element_type: str
    label: str
    count: int = 1

class InventoryView(BaseModel):
    profile_name: str
    elements: List[InventoryItem]

class TechnicalElement(BaseModel):
    element_type: str
    label: str
    pos_x_cm: float
    pos_z_cm: float
    angle_deg: float
    height_cm: float

class TechnicalView(BaseModel):
    profile_name: str
    elements: List[TechnicalElement]

# In-memory token store for now
# In production this would be a database table
share_tokens = {}

@router.post("/generate", response_model=ShareLinkResponse)
def generate_share_link(request: ShareLinkCreate, db: Session = Depends(get_db)):
    profile = db.query(KitProfile).filter(
        KitProfile.id == request.profile_id).first()
    if not profile:
        raise HTTPException(status_code=404, detail="Profile not found")

    token = secrets.token_urlsafe(16)
    expires_at = datetime.datetime.utcnow() + datetime.timedelta(
        hours=request.expires_hours)

    share_tokens[token] = {
        "profile_id": str(request.profile_id),
        "view_level": request.view_level,
        "expires_at": expires_at
    }

    return ShareLinkResponse(
        token=token,
        view_level=request.view_level,
        expires_at=expires_at,
        share_url=f"/shares/view/{token}"
    )

@router.get("/view/{token}")
def view_shared_kit(token: str, db: Session = Depends(get_db)):
    if token not in share_tokens:
        raise HTTPException(status_code=404, detail="Share link not found or expired")

    token_data = share_tokens[token]

    if datetime.datetime.utcnow() > token_data["expires_at"]:
        del share_tokens[token]
        raise HTTPException(status_code=410, detail="Share link has expired")

    profile_id = uuid.UUID(token_data["profile_id"])
    view_level = token_data["view_level"]

    profile = db.query(KitProfile).filter(KitProfile.id == profile_id).first()
    if not profile:
        raise HTTPException(status_code=404, detail="Profile not found")

    elements = db.query(KitElement).filter(
        KitElement.profile_id == profile_id).all()

    if view_level == ViewLevel.footprint:
        return FootprintView(
            profile_name=profile.name,
            stage_width_cm=profile.stage_width_cm,
            stage_depth_cm=profile.stage_depth_cm,
            element_count=len(elements)
        )

    elif view_level == ViewLevel.inventory:
        inventory = {}
        for elem in elements:
            key = elem.element_type.value
            if key in inventory:
                inventory[key]["count"] += 1
            else:
                inventory[key] = {
                    "element_type": key,
                    "label": elem.label,
                    "count": 1
                }
        return InventoryView(
            profile_name=profile.name,
            elements=[InventoryItem(**v) for v in inventory.values()]
        )

    elif view_level == ViewLevel.technical:
        return TechnicalView(
            profile_name=profile.name,
            elements=[TechnicalElement(
                element_type=e.element_type.value,
                label=e.label,
                pos_x_cm=e.pos_x_cm,
                pos_z_cm=e.pos_z_cm,
                angle_deg=e.angle_deg,
                height_cm=e.height_cm
            ) for e in elements]
        )

    else:  # full
        return {
            "profile_name": profile.name,
            "description": profile.description,
            "stage_width_cm": profile.stage_width_cm,
            "stage_depth_cm": profile.stage_depth_cm,
            "elements": [{
                "element_type": e.element_type.value,
                "label": e.label,
                "pos_x_cm": e.pos_x_cm,
                "pos_y_cm": e.pos_y_cm,
                "pos_z_cm": e.pos_z_cm,
                "angle_deg": e.angle_deg,
                "height_cm": e.height_cm
            } for e in elements]
        }