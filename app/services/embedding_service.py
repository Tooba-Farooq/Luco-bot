from deepface import DeepFace

FACE_RECOGNITION_MODEL = "Facenet"
FACE_DETECTOR_BACKEND = "mediapipe"

def generate_face_embedding(photo_path: str):
    """
    Runs DeepFace to compute a face embedding from a saved photo.
    Returns the embedding as a list (JSON-serializable), or None if it fails.
    """
    try:
        result = DeepFace.represent(
            img_path=photo_path,
            model_name=FACE_RECOGNITION_MODEL,
            detector_backend=FACE_DETECTOR_BACKEND,
            enforce_detection=True
        )
        embedding = result[0]["embedding"]
        return embedding
    except Exception as e:
        print(f"Embedding generation failed: {e}")
        return None