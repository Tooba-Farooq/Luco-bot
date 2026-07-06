from fastapi import APIRouter, UploadFile, File
from app.models import DetectionResponse
import time

def check_face_present(image_bytes) -> (bool, tuple):
    """
    Placeholder function to check if a face is present in the image.
    Returns a tuple: (face_found: bool, face_box: tuple)
    """
    # Implement using OpenCV or MediaPipe
    # For now, return dummy values
    return True, (0, 0, 100, 100)  # Example bounding box

def check_face_forward(image_bytes, face_box) -> bool:
    """
    Placeholder function to check if the face is forward-facing.
    Returns a boolean indicating if the face is forward-facing.
    """
    # Implement using OpenCV or MediaPipe
    # For now, return a dummy value
    return True

router = APIRouter()

# in-memory timer state for now (single tablet = fine; move to DB/session later)
forward_start_time = None

@router.post("/detect", response_model=DetectionResponse)
async def detect(frame: UploadFile = File(...)):
    image_bytes = await frame.read()
    
    # STEP 1: cheap face detection check
    face_found, face_box = check_face_present(image_bytes)  # you'll write this using OpenCV/MediaPipe
    if not face_found:
        global forward_start_time
        forward_start_time = None
        return DetectionResponse(status="idle")
    
    # STEP 2: forward-facing check
    is_forward = check_face_forward(image_bytes, face_box)  # yaw angle from landmarks
    global forward_start_time
    if is_forward:
        if forward_start_time is None:
            forward_start_time = time.time()
        duration = time.time() - forward_start_time
    else:
        forward_start_time = None
        duration = 0.0
    
    if duration < 3.0:
        return DetectionResponse(status="detecting", face_forward=is_forward, forward_duration=duration)
    
    # STEP 3: only now run face recognition (expensive)
    name, confidence = run_face_recognition(image_bytes)  # you'll write this using face_recognition lib
    if name:
        return DetectionResponse(status="known", visitor_name=name, confidence=confidence, face_forward=True, forward_duration=duration)
    else:
        return DetectionResponse(status="unknown", face_forward=True, forward_duration=duration)