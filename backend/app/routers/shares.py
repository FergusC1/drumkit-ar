# shares.py
# Generates and validates share links for kit profiles.
# Each link is tied to a view level that controls which fields are returned,
# implementing the role-based data access identified in the requirements research.
#
# View levels and their intended stakeholders:
#   footprint  - promoters/venue managers (stage dimensions and element count only)
#   technical  - sound engineers (positions, angles, heights)
#   inventory  - backline companies (element types and counts, no spatial data)
#   full       - drummers and trusted collaborators (all fields)

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


# --- Enums and request/response models ---

class ViewLevel(str, enum.Enum):
    full = "full"
    technical = "technical"
    footprint = "footprint"
    inventory = "inventory"

class ShareLinkCreate(BaseModel):
    profile_id: uuid.UUID
    view_level: ViewLevel
    expires_hours: int = 24  # Default expiry of 24 hours

class ShareLinkResponse(BaseModel):
    token: str
    view_level: ViewLevel
    expires_at: datetime.datetime
    share_url: str

# Each view level has its own response model exposing only the relevant fields

class FootprintView(BaseModel):
    profile_name: str
    stage_width_cm: Optional[float]
    stage_depth_cm: Optional[float]
    element_count: int  # Count only - no element types or positions

class InventoryItem(BaseModel):
    element_type: str
    label: str
    count: int = 1

class InventoryView(BaseModel):
    profile_name: str
    elements: List[InventoryItem]  # Types and counts, no spatial data

class TechnicalElement(BaseModel):
    element_type: str
    label: str
    pos_x_cm: float
    pos_z_cm: float   # Y omitted - sound engineers need floor position not height
    angle_deg: float
    height_cm: float

class TechnicalView(BaseModel):
    profile_name: str
    elements: List[TechnicalElement]


# In-memory token store.
# Tokens are stored as a dict keyed by the token string.
# Known limitation: tokens are lost if the server restarts.
# A production implementation would persist tokens in a ShareLink database table.
share_tokens = {}


# POST /shares/generate
# Creates a share token linked to a profile and view level.
# Uses Python's secrets module for cryptographically secure token generation.
@router.post("/generate", response_model=ShareLinkResponse)
def generate_share_link(request: ShareLinkCreate, db: Session = Depends(get_db)):
    # Verify the profile exists before generating a token
    profile = db.query(KitProfile).filter(
        KitProfile.id == request.profile_id).first()
    if not profile:
        raise HTTPException(status_code=404, detail="Profile not found")

    # token_urlsafe generates a URL-safe base64 string - safe to use in URLs
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


# GET /shares/view/{token}
# Returns kit data filtered to the view level associated with the token.
# Returns 404 if the token is unknown and 410 Gone if it has expired.
# The response structure varies by view level - each stakeholder type
# receives only the fields relevant to their workflow.
@router.get("/view/{token}")
def view_shared_kit(token: str, db: Session = Depends(get_db)):
    if token not in share_tokens:
        raise HTTPException(status_code=404, detail="Share link not found or expired")

    token_data = share_tokens[token]

    # Check expiry and clean up expired tokens
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

    # --- View level filtering ---
    # Each branch returns only the fields appropriate for that stakeholder type

    if view_level == ViewLevel.footprint:
        # Promoters only need to know the stage footprint and how many drums there are
        return FootprintView(
            profile_name=profile.name,
            stage_width_cm=profile.stage_width_cm,
            stage_depth_cm=profile.stage_depth_cm,
            element_count=len(elements)
        )

    elif view_level == ViewLevel.inventory:
        # Backline companies need element types and counts to source equivalent equipment.
        # Elements of the same type are grouped and counted.
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
        # Sound engineers need positions and angles to plan mic placement and cable runs
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

    else:  # full - all fields returned for drummers and trusted collaborators
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