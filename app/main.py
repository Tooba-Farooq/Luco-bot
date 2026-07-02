from fastapi import FastAPI

app = FastAPI(title="Reception Robot Backend")

@app.get("/")
def health_check():
    return {"status": "ok"}