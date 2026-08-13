import cv2
import numpy as np
from insightface.app import FaceAnalysis

# --- Recognition config (validated: InsightFace buffalo_l, see benchmark results —
# 100% match rate, 0% false accept on internal test set, vs. 95.8%/5.6% on the
# previous DeepFace ArcFace+yunet setup) ---
FACE_ANALYSIS_PACK = "buffalo_l"  # SCRFD detector + ArcFace r100 recognizer (InsightFace's
                                  # current recommended general-purpose pack)

# ctx_id=-1 forces CPU explicitly (avoids the "CUDAExecutionProvider not available"
# warning on machines without a GPU — we're CPU-only for now)
_face_app = FaceAnalysis(name=FACE_ANALYSIS_PACK)
_face_app.prepare(ctx_id=-1, det_size=(640, 640))


def _get_largest_face(faces):
    """If multiple faces are detected in one frame, take the largest
    (closest to camera) — same convention used in the benchmark script."""
    if not faces:
        return None
    faces.sort(key=lambda f: (f.bbox[2] - f.bbox[0]) * (f.bbox[3] - f.bbox[1]), reverse=True)
    return faces[0]


def get_embedding_from_array(image: np.ndarray):
    """
    Core embedding function — takes an already-decoded BGR numpy array
    (matches the shape used elsewhere in the pipeline, e.g. _load_image()
    in detection_service.py) and returns a normalized 512-d embedding
    (numpy array), or None if no face was detected.
    """
    if image is None:
        return None

    faces = _face_app.get(image)
    face = _get_largest_face(faces)
    if face is None:
        return None

    return face.normed_embedding


def generate_face_embedding(photo_path: str):
    """
    Runs InsightFace to compute a face embedding from a saved photo.
    Returns the embedding as a list (JSON-serializable, matches the
    existing Visitor.face_embedding JSON column), or None if it fails.
    """
    try:
        image = cv2.imread(photo_path)
        if image is None:
            print(f"Embedding generation failed: could not read {photo_path}")
            return None

        embedding = get_embedding_from_array(image)
        if embedding is None:
            print(f"Embedding generation failed: no face detected in {photo_path}")
            return None

        return embedding.tolist()
    except Exception as e:
        print(f"Embedding generation failed: {e}")
        return None