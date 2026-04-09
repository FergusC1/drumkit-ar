from fastapi import FastAPI

app = FastAPI(title="DrumKit AR API")

@app.get("/health")
def health():
    return {"status": "ok"}