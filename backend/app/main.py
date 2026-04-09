from fastapi import FastAPI
from app.routers import router as users_router

app = FastAPI(title="DrumKit AR API")

app.include_router(users_router)

@app.get("/health")
def health():
    return {"status": "ok"}