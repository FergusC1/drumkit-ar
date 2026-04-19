from fastapi import FastAPI
from app.routers import router as users_router
from app.routers.profiles import router as profiles_router
from app.routers.elements import router as elements_router

app = FastAPI(title="DrumKit AR API")

app.include_router(users_router)
app.include_router(profiles_router)
app.include_router(elements_router)

@app.get("/health")
def health():
    return {"status": "ok"}