import cv2
import numpy as np
from insightface.app import FaceAnalysis

# --- Setup: same model pack used in the benchmark (buffalo_l = SCRFD + ArcFace r100) ---
app = FaceAnalysis(name="buffalo_l")
app.prepare(ctx_id=0, det_size=(640, 640))  # ctx_id=-1 forces CPU if no GPU available


def generate_face_embedding_insightface(image_path):
    """Drop-in replacement for your current generate_face_embedding(), but
    using InsightFace instead of DeepFace/ArcFace. Returns a normalized
    512-d embedding, or None if no face was detected."""
    img = cv2.imread(image_path)
    if img is None:
        print(f"  WARNING: could not read {image_path}")
        return None

    faces = app.get(img)
    if not faces:
        print(f"  WARNING: no face detected in {image_path}")
        return None

    # if multiple faces detected, take the largest (closest to camera) —
    # same convention as the benchmark script
    faces.sort(key=lambda f: (f.bbox[2] - f.bbox[0]) * (f.bbox[3] - f.bbox[1]), reverse=True)
    return faces[0].normed_embedding


def cosine_similarity(a, b):
    """InsightFace's native comparison metric — HIGHER means more similar,
    opposite direction from DeepFace's cosine DISTANCE (lower = more similar).
    Don't compare these raw numbers against your old thresholds."""
    if a is None or b is None:
        return None
    return float(np.dot(a, b))


# --- Same photos as your manual test, generating embeddings via InsightFace ---
you_photo_1 = generate_face_embedding_insightface(r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260726_23_02_54_Pro.jpg")  # original photo
you_photo_2 = generate_face_embedding_insightface(r"D:\ReactDjango projects\Lucobot_backend\visitor_photos\2766445fb87c4d0bbfccf4558650fd73.jpg")   # different photo, same person
ayesha_photo_1 = generate_face_embedding_insightface(r"C:\Users\tooba\Downloads\WhatsApp Image 2026-07-23 at 12.37.42 PM (1).jpeg")  # different person
ayesha_photo_2 = generate_face_embedding_insightface(r"C:\Users\tooba\Downloads\WhatsApp Image 2026-07-23 at 11.43.07 AM (1).jpeg")  # different person
abba_photo_1 = generate_face_embedding_insightface(r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260726_23_16_39_Pro.jpg")  # different person
abba_photo_2 = generate_face_embedding_insightface(r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260726_22_14_21_Pro.jpg")  # different person
mother = generate_face_embedding_insightface(r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260726_22_20_06_Pro.jpg")  # different person
sister_1 = generate_face_embedding_insightface(r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260726_22_29_25_Pro.jpg")  # different person
furqan = generate_face_embedding_insightface(r"D:\ReactDjango projects\Lucobot_backend\visitor_photos\0b2b5efd03884f0e8737f29068bcd071.jpg")  # different person
sister_2 = generate_face_embedding_insightface(r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260726_23_33_15_Pro.jpg")  # different person
ayesha_3 = generate_face_embedding_insightface(r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260728_11_30_12_Pro.jpg")  # different person


pairs = [
    ("you vs you (different photo)",      you_photo_1, you_photo_2,     "genuine"),
    ("you vs mother",                     you_photo_1, mother,          "impostor"),
    ("you vs sister_1",                   you_photo_1, sister_1,        "impostor"),
    ("sister vs sister",                  sister_1, sister_2,           "genuine"),
    ("you vs furqan",                     you_photo_1, furqan,          "impostor"),
    ("ayesha vs ayesha (different photo)", ayesha_photo_1, ayesha_photo_2, "genuine"),
    ("abba vs abba",                      abba_photo_1, abba_photo_2,   "genuine"),
    ("furqan vs sister_1",                furqan, sister_1,             "impostor"),
    ("furqan vs sister_2",                furqan, sister_2,             "impostor"),
    ("ayesha vs ayesha (third photo)",    ayesha_photo_1, ayesha_3,     "genuine"),
]

print("\n=== Pairwise similarity (InsightFace, buffalo_l) ===")
genuine_scores = []
impostor_scores = []

for label, emb_a, emb_b, pair_type in pairs:
    sim = cosine_similarity(emb_a, emb_b)
    if sim is None:
        print(f"{label}: SKIPPED (missing embedding)")
        continue
    print(f"{label}: {sim:.3f}  [{pair_type}]")
    if pair_type == "genuine":
        genuine_scores.append(sim)
    else:
        impostor_scores.append(sim)

# --- Threshold suggestion ---
if genuine_scores and impostor_scores:
    lowest_genuine = min(genuine_scores)
    highest_impostor = max(impostor_scores)

    print(f"\n--- Summary ---")
    print(f"Lowest genuine similarity:   {lowest_genuine:.3f}")
    print(f"Highest impostor similarity: {highest_impostor:.3f}")

    if lowest_genuine > highest_impostor:
        margin = lowest_genuine - highest_impostor
        suggested = highest_impostor + margin / 2
        print(f"Clean separation — margin of {margin:.3f}")
        print(f"Suggested threshold (midpoint): {suggested:.3f}")
    else:
        print(f"WARNING: overlap detected — lowest genuine ({lowest_genuine:.3f}) is "
              f"BELOW highest impostor ({highest_impostor:.3f}).")
        print(f"No single threshold cleanly separates these pairs — collect more "
              f"data or review the overlapping photos (lighting/angle/occlusion?).")
else:
    print("\nNot enough genuine/impostor pairs to suggest a threshold.")