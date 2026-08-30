# import os
# import time
# from deepface import DeepFace
# import traceback
# import numpy as np

# # --- CONFIG: point these at your test image folders ---
# # structure expected:
# # test_data/known/person1_ref.jpg   <- one reference photo per person
# # test_data/known/person2_ref.jpg
# # test_data/probe/person1_a.jpg     <- test photos, same people, different shots
# # test_data/probe/person1_b.jpg
# # test_data/probe/person2_a.jpg
# # test_data/strangers/unknown1.jpg  <- people NOT in known/, for false-accept testing

# KNOWN_DIR = "test_data/known"
# PROBE_DIR = "test_data/probe"
# STRANGER_DIR = "test_data/strangers"

# MODELS_TO_TEST = [
#     # ("Facenet", "retinaface"),
#     # ("SFace", "retinaface"),
#     # ("Facenet512", "retinaface"),
#     # ("ArcFace", "opencv"),
#     # ("GhostFaceNet", "opencv"),
#     # ("ArcFace", "yunet"),
#     # ("ArcFace", "mediapipe"),
#     # ("ArcFace", "ssd"),
# ]

# # Set to True to also run the native InsightFace benchmark below,
# # alongside whatever DeepFace combos are enabled above.
# RUN_INSIGHTFACE = True

# # InsightFace similarity threshold to test (cosine similarity — HIGHER means
# # more similar, opposite direction from DeepFace's "distance" metric).
# # 0.5-0.6 is a common starting point for buffalo_l; tune based on your results
# # the same way you'd tune ArcFace's distance threshold.
# INSIGHTFACE_THRESHOLD = 0.5


# def get_person_name(filename):
#     return filename.split("_")[0]


# def find_reference_photo(person_name):
#     """Looks for {person}_ref.<any common extension>"""
#     for ext in ["jpg", "jpeg", "png"]:
#         candidate = os.path.join(KNOWN_DIR, f"{person_name}_ref.{ext}")
#         if os.path.exists(candidate):
#             return candidate
#     return None


# def run_test(model_name, detector_backend):
#     print(f"\n=== Testing {model_name} + {detector_backend} ===")

#     known_files = [f for f in os.listdir(KNOWN_DIR) if f.endswith(('.jpg', '.png'))]
#     probe_files = [f for f in os.listdir(PROBE_DIR) if f.endswith(('.jpg', '.png'))]
#     stranger_files = [f for f in os.listdir(STRANGER_DIR) if f.endswith(('.jpg', '.png'))]

#     correct_matches = 0
#     total_same_person_tests = 0
#     false_accepts = 0
#     total_stranger_tests = 0
#     total_time = 0
#     error_count = 0

#     for probe_file in probe_files:
#         person = get_person_name(probe_file)
#         known_path = find_reference_photo(person)
#         probe_path = os.path.join(PROBE_DIR, probe_file)

#         if known_path is None:
#             print(f"  Skipping {probe_file}: no matching reference found")
#             continue

#         try:
#             start = time.time()
#             result = DeepFace.verify(
#                 img1_path=known_path,
#                 img2_path=probe_path,
#                 model_name=model_name,
#                 detector_backend=detector_backend,
#                 enforce_detection=False
#             )
#             elapsed = time.time() - start
#             total_time += elapsed
#             total_same_person_tests += 1

#             if result["verified"]:
#                 correct_matches += 1
#             print(f"  {probe_file} vs {os.path.basename(known_path)}: verified={result['verified']}, distance={result['distance']:.3f}, time={elapsed:.2f}s")
#         except Exception as e:
#             error_count += 1
#             print(f"  ERROR on {probe_file}: {e}")
#             traceback.print_exc()

#     for stranger_file in stranger_files:
#         stranger_path = os.path.join(STRANGER_DIR, stranger_file)
#         for known_file in known_files:
#             known_path = os.path.join(KNOWN_DIR, known_file)
#             try:
#                 result = DeepFace.verify(
#                     img1_path=known_path,
#                     img2_path=stranger_path,
#                     model_name=model_name,
#                     detector_backend=detector_backend,
#                     enforce_detection=False
#                 )
#                 total_stranger_tests += 1
#                 if result["verified"]:
#                     false_accepts += 1
#                     print(f"  FALSE ACCEPT: {stranger_file} matched {known_file} (distance={result['distance']:.3f})")
#             except Exception as e:
#                 error_count += 1

#     accuracy = (correct_matches / total_same_person_tests * 100) if total_same_person_tests else 0
#     false_accept_rate = (false_accepts / total_stranger_tests * 100) if total_stranger_tests else 0
#     avg_time = (total_time / total_same_person_tests) if total_same_person_tests else 0

#     print(f"\n  RESULTS for {model_name} + {detector_backend}:")
#     print(f"  Correct match rate: {accuracy:.1f}% ({correct_matches}/{total_same_person_tests})")
#     print(f"  False accept rate: {false_accept_rate:.1f}% ({false_accepts}/{total_stranger_tests})")
#     print(f"  Avg time per comparison: {avg_time:.2f}s")
#     print(f"  Errors: {error_count}")

#     return {
#         "model": model_name,
#         "detector": detector_backend,
#         "accuracy": accuracy,
#         "false_accept_rate": false_accept_rate,
#         "avg_time": avg_time,
#         "errors": error_count
#     }


# def run_insightface_test(threshold=INSIGHTFACE_THRESHOLD):
#     """
#     Native InsightFace benchmark — uses the buffalo_l model pack
#     (SCRFD detector + ArcFace r100 recognizer) via onnxruntime, NOT
#     DeepFace's wrapper. This is what actually reflects InsightFace's
#     real speed/accuracy, since DeepFace's own "retinaface" backend
#     is a much slower reimplementation.
#     """
#     from insightface.app import FaceAnalysis

#     print(f"\n=== Testing InsightFace (buffalo_l) — threshold={threshold} ===")

#     app = FaceAnalysis(name="buffalo_l")
#     app.prepare(ctx_id=0, det_size=(640, 640))  # ctx_id=-1 forces CPU if no GPU available

#     def get_embedding(img_path):
#         import cv2
#         img = cv2.imread(img_path)
#         if img is None:
#             return None
#         faces = app.get(img)
#         if not faces:
#             return None
#         # if multiple faces detected, take the largest (closest to camera)
#         faces.sort(key=lambda f: (f.bbox[2] - f.bbox[0]) * (f.bbox[3] - f.bbox[1]), reverse=True)
#         return faces[0].normed_embedding

#     def cosine_similarity(a, b):
#         return float(np.dot(a, b))

#     known_files = [f for f in os.listdir(KNOWN_DIR) if f.endswith(('.jpg', '.png'))]
#     probe_files = [f for f in os.listdir(PROBE_DIR) if f.endswith(('.jpg', '.png'))]
#     stranger_files = [f for f in os.listdir(STRANGER_DIR) if f.endswith(('.jpg', '.png'))]

#     # pre-compute known reference embeddings once, so timing per comparison
#     # reflects real runtime use (embed once at registration, compare many times)
#     known_embeddings = {}
#     for known_file in known_files:
#         known_path = os.path.join(KNOWN_DIR, known_file)
#         emb = get_embedding(known_path)
#         if emb is not None:
#             known_embeddings[known_file] = emb
#         else:
#             print(f"  WARNING: no face detected in {known_file}")

#     correct_matches = 0
#     total_same_person_tests = 0
#     false_accepts = 0
#     total_stranger_tests = 0
#     total_time = 0
#     error_count = 0

#     for probe_file in probe_files:
#         person = get_person_name(probe_file)
#         known_path = find_reference_photo(person)
#         probe_path = os.path.join(PROBE_DIR, probe_file)

#         if known_path is None:
#             print(f"  Skipping {probe_file}: no matching reference found")
#             continue

#         known_file = os.path.basename(known_path)
#         if known_file not in known_embeddings:
#             error_count += 1
#             print(f"  ERROR on {probe_file}: no embedding for reference {known_file}")
#             continue

#         try:
#             start = time.time()
#             probe_emb = get_embedding(probe_path)
#             elapsed = time.time() - start

#             if probe_emb is None:
#                 error_count += 1
#                 print(f"  ERROR on {probe_file}: no face detected")
#                 continue

#             total_time += elapsed
#             total_same_person_tests += 1

#             sim = cosine_similarity(known_embeddings[known_file], probe_emb)
#             verified = sim >= threshold
#             if verified:
#                 correct_matches += 1
#             print(f"  {probe_file} vs {known_file}: verified={verified}, similarity={sim:.3f}, time={elapsed:.2f}s")
#         except Exception as e:
#             error_count += 1
#             print(f"  ERROR on {probe_file}: {e}")
#             traceback.print_exc()

#     for stranger_file in stranger_files:
#         stranger_path = os.path.join(STRANGER_DIR, stranger_file)
#         stranger_emb = get_embedding(stranger_path)
#         if stranger_emb is None:
#             error_count += 1
#             continue

#         for known_file, known_emb in known_embeddings.items():
#             try:
#                 total_stranger_tests += 1
#                 sim = cosine_similarity(known_emb, stranger_emb)
#                 if sim >= threshold:
#                     false_accepts += 1
#                     print(f"  FALSE ACCEPT: {stranger_file} matched {known_file} (similarity={sim:.3f})")
#             except Exception as e:
#                 error_count += 1

#     accuracy = (correct_matches / total_same_person_tests * 100) if total_same_person_tests else 0
#     false_accept_rate = (false_accepts / total_stranger_tests * 100) if total_stranger_tests else 0
#     avg_time = (total_time / total_same_person_tests) if total_same_person_tests else 0

#     print(f"\n  RESULTS for InsightFace (buffalo_l):")
#     print(f"  Correct match rate: {accuracy:.1f}% ({correct_matches}/{total_same_person_tests})")
#     print(f"  False accept rate: {false_accept_rate:.1f}% ({false_accepts}/{total_stranger_tests})")
#     print(f"  Avg time per comparison: {avg_time:.2f}s")
#     print(f"  Errors: {error_count}")

#     return {
#         "model": "InsightFace",
#         "detector": "buffalo_l (SCRFD)",
#         "accuracy": accuracy,
#         "false_accept_rate": false_accept_rate,
#         "avg_time": avg_time,
#         "errors": error_count
#     }


# if __name__ == "__main__":
#     all_results = []
#     for model_name, detector in MODELS_TO_TEST:
#         result = run_test(model_name, detector)
#         all_results.append(result)

#     if RUN_INSIGHTFACE:
#         try:
#             result = run_insightface_test()
#             all_results.append(result)
#         except ImportError:
#             print("\nInsightFace not installed. Run: pip install insightface onnxruntime")

#     print("\n\n=== SUMMARY (ranked by accuracy) ===")
#     all_results.sort(key=lambda r: r["accuracy"], reverse=True)
#     for r in all_results:
#         print(f"{r['model']:15} {r['detector']:20} accuracy={r['accuracy']:.1f}%  false_accept={r['false_accept_rate']:.1f}%  avg_time={r['avg_time']:.2f}s  errors={r['errors']}")


import os
import time
from deepface import DeepFace
import traceback

# --- CONFIG: point these at your test image folders ---
# structure expected:
# test_data/known/person1_ref.jpg   <- one reference photo per person
# test_data/known/person2_ref.jpg
# test_data/probe/person1_a.jpg     <- test photos, same people, different shots
# test_data/probe/person1_b.jpg
# test_data/probe/person2_a.jpg
# test_data/strangers/unknown1.jpg  <- people NOT in known/, for false-accept testing

KNOWN_DIR = "test_data/known"
PROBE_DIR = "test_data/probe"
STRANGER_DIR = "test_data/strangers"

MODELS_TO_TEST = [
    # ("Facenet", "retinaface"),
    # ("SFace", "retinaface"),
    # ("Facenet512", "retinaface"),
    # ("ArcFace", "opencv"),
    # ("GhostFaceNet", "opencv"),
    ("ArcFace", "yunet"),
    ("ArcFace", "mediapipe"),
    ("ArcFace", "ssd"),
]

def get_person_name(filename):
    return filename.split("_")[0]


def find_reference_photo(person_name):
    """Looks for {person}_ref.<any common extension>"""
    for ext in ["jpg", "jpeg", "png"]:
        candidate = os.path.join(KNOWN_DIR, f"{person_name}_ref.{ext}")
        if os.path.exists(candidate):
            return candidate
    return None

def run_test(model_name, detector_backend):
    print(f"\n=== Testing {model_name} + {detector_backend} ===")

    known_files = [f for f in os.listdir(KNOWN_DIR) if f.endswith(('.jpg', '.png'))]
    probe_files = [f for f in os.listdir(PROBE_DIR) if f.endswith(('.jpg', '.png'))]
    stranger_files = [f for f in os.listdir(STRANGER_DIR) if f.endswith(('.jpg', '.png'))]

    correct_matches = 0
    total_same_person_tests = 0
    false_accepts = 0
    total_stranger_tests = 0
    total_time = 0
    error_count = 0

    # test: probe photos should match their corresponding known reference
    for probe_file in probe_files:
        person = get_person_name(probe_file)
        known_path = find_reference_photo(person)
        probe_path = os.path.join(PROBE_DIR, probe_file)

        if known_path is None:
            print(f"  Skipping {probe_file}: no matching reference found")
            continue

        try:
            start = time.time()
            result = DeepFace.verify(
                img1_path=known_path,
                img2_path=probe_path,
                model_name=model_name,
                detector_backend=detector_backend,
                enforce_detection=False
            )
            elapsed = time.time() - start
            total_time += elapsed
            total_same_person_tests += 1

            if result["verified"]:
                correct_matches += 1
            print(f"  {probe_file} vs {os.path.basename(known_path)}: verified={result['verified']}, distance={result['distance']:.3f}, time={elapsed:.2f}s")
        except Exception as e:
            error_count += 1
            print(f"  ERROR on {probe_file}: {e}")
            traceback.print_exc()   # add this line temporarily
        except Exception as e:
            error_count += 1
            print(f"  ERROR on {probe_file}: {e}")
            traceback.print_exc()   # add this line temporarily

     # test: stranger photos should NOT match any known reference
    for stranger_file in stranger_files:
        stranger_path = os.path.join(STRANGER_DIR, stranger_file)
        for known_file in known_files:
            known_path = os.path.join(KNOWN_DIR, known_file)
            try:
                result = DeepFace.verify(
                    img1_path=known_path,
                    img2_path=stranger_path,
                    model_name=model_name,
                    detector_backend=detector_backend,
                    enforce_detection=False
                )
                total_stranger_tests += 1
                if result["verified"]:
                    false_accepts += 1
                    print(f"  FALSE ACCEPT: {stranger_file} matched {known_file} (distance={result['distance']:.3f})")
            except Exception as e:
                error_count += 1

    accuracy = (correct_matches / total_same_person_tests * 100) if total_same_person_tests else 0
    false_accept_rate = (false_accepts / total_stranger_tests * 100) if total_stranger_tests else 0
    avg_time = (total_time / total_same_person_tests) if total_same_person_tests else 0

    print(f"\n  RESULTS for {model_name} + {detector_backend}:")
    print(f"  Correct match rate: {accuracy:.1f}% ({correct_matches}/{total_same_person_tests})")
    print(f"  False accept rate: {false_accept_rate:.1f}% ({false_accepts}/{total_stranger_tests})")
    print(f"  Avg time per comparison: {avg_time:.2f}s")
    print(f"  Errors: {error_count}")

    return {
        "model": model_name,
        "detector": detector_backend,
        "accuracy": accuracy,
        "false_accept_rate": false_accept_rate,
        "avg_time": avg_time,
        "errors": error_count
    }

if __name__ == "__main__":
    all_results = []
    for model_name, detector in MODELS_TO_TEST:
        result = run_test(model_name, detector)
        all_results.append(result)

    print("\n\n=== SUMMARY (ranked by accuracy) ===")
    all_results.sort(key=lambda r: r["accuracy"], reverse=True)
    for r in all_results:
        print(f"{r['model']:15} accuracy={r['accuracy']:.1f}%  false_accept={r['false_accept_rate']:.1f}%  avg_time={r['avg_time']:.2f}s  errors={r['errors']}")