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
    # ("ArcFace", "yunet"),
    # ("ArcFace", "mediapipe"),
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