# Lucobot Backend — Technology Stack

## FastAPI
Selected as the backend framework due to the large number of interdependent components in the system. FastAPI's asynchronous support and reduced boilerplate allowed for faster development without compromising structure.

## Pydantic
Used as FastAPI's built-in data validation layer, ensuring strict validation of request and response data across all endpoints.

## SQLAlchemy (Async)
Adopted early in development to keep the database layer abstracted from the underlying engine, allowing a future transition from SQLite to a different production database without major rework.

## SQLite (Development)
Used during early development and testing as a lightweight, file-based database suited to local iteration.

## Alembic
Introduced during development once schema changes (e.g., adding new columns) became necessary. Alembic enabled version-controlled schema migrations without requiring the database to be dropped and recreated, preserving existing data.

## Neon PostgreSQL (Production Database)
Adopted for production to move beyond the limitations of a local file-based database. Supabase was considered, but since authentication was already implemented independently, Neon was selected for its managed PostgreSQL offering and generous free tier suited to the project's scale.

---

## Pre-Detection Pipeline (Two-Stage Lightweight Checks)

### Face Presence Check — MediaPipe BlazeFace (Short Range, `blaze_face_short_range.tflite`)
A lightweight face detector is run first on every incoming frame to confirm whether a face is present at all. This is the cheap, high-frequency check that runs continuously.

### Head-Pose Check — MediaPipe Face Landmarker (`face_landmarker.task`)
Only if a face is detected does the heavier Face Landmarker model run, extracting facial transformation data used to calculate the yaw angle and determine whether the visitor is facing the camera.

---

## Face Recognition — InsightFace (buffalo_l: SCRFD + ArcFace r100)

Selected after benchmarking multiple face detection and recognition combinations on a custom dataset (reference + test photos per person, plus stranger photos for false-accept testing) built to reflect the system's actual camera and lighting conditions.

### Benchmark Results (DeepFace combinations)

| Model + Detector | Accuracy | False Accept Rate | Avg Time/Comparison |
|---|---|---|---|
| Facenet + RetinaFace | 83.3% | 5.6% | 94.23s |
| SFace + RetinaFace | 70.8% | 0.0% | ~80–120s |
| Facenet512 + RetinaFace | 62.5% | 0.0% | 100.79s |
| ArcFace + OpenCV | 95.8% | 0.0% | 2.10s |
| GhostFaceNet + OpenCV | 83.3% | 0.0% | 2.62s |
| ArcFace + YuNet | 95.8% | 5.6% | 2.41s |
| ArcFace + MediaPipe | Failed to run (library version mismatch) | — | — |
| ArcFace + SSD | 79.2% | 0.0% | 2.00s |

RetinaFace-based combinations were too slow for real-time use (95s–several minutes per comparison). ArcFace was consistently the strongest recognition model across all detectors tested, making it the clear choice among DeepFace options.

### Detector Selection and Migration Path

ArcFace + YuNet initially produced a false positive on the small benchmark set, so ArcFace + OpenCV was chosen instead. However, OpenCV's detector proved unreliable in early testing — it occasionally failed to detect a clearly visible face entirely, meaning that visitor's embedding was never captured and they could never be recognized later. Since a missed registration is a more serious, permanent failure than an occasional misidentification, ArcFace + YuNet was adopted instead and used through a period of active development.

As the visitor database grew, YuNet's false-positive rate became unacceptable at scale (e.g., one visitor was repeatedly misidentified as another due to facial hair), and raising the threshold to fix it caused known visitors to be rejected instead. This prompted a final evaluation of InsightFace.

### Final Result — InsightFace (buffalo_l)

| Model | Accuracy | False Accept Rate | Avg Time/Comparison |
|---|---|---|---|
| InsightFace (buffalo_l) | 100.0% (24/24) | 0.0% (0/18) | 1.88s |

InsightFace outperformed every prior combination on both accuracy and speed, and was adopted as the production pipeline, performing reliably through the rest of development and all demos.

### Recognition Threshold
Set at 0.515 (cosine similarity), calibrated via `calculate_distance.py` at the midpoint of a clean 0.35 gap between the lowest genuine-match similarity (0.690) and highest impostor similarity (0.340), prioritizing zero false accepts.

### Licensing Note
InsightFace's code is MIT-licensed, but the buffalo_l pretrained models are restricted to non-commercial research use; commercial deployment requires a separate license from InsightFace. As an academic Final Year Project, current use falls within the non-commercial research terms.

---

## Groq — Whisper (Speech-to-Text)
Selected for its free-tier availability and ability to transcribe mixed Urdu-English speech, matching the project's real-world usage context. For general speech transcription, audio is transcribed twice in parallel — once forced to English, once forced to Urdu — and the transcription with the higher confidence score is used downstream. This approach was adopted after testing automatic language detection, which was unreliable and occasionally returned transcriptions in entirely unrelated languages.

## Name Capture (Groq Whisper + LLM Resolution)
For capturing a visitor's spoken name specifically, both the English-forced and Urdu-forced transcriptions are generated, since testing showed that neither one consistently outperformed the other — sometimes English-forced transcription produced a better result, sometimes Urdu-forced did. Both transcriptions are then passed to an LLM with a prompt indicating that the name is likely Pakistani or Muslim in origin, and the LLM is asked to determine the most sensible name based on both versions. This resolves cases where neither individual transcription alone is fully accurate.

## Groq — openai/gpt-oss-20b (Intent Classification and Other LLM Tasks)
Originally implemented using Llama 3.1-8b-instant for intent classification and related language-processing tasks. Following Groq's decommissioning of that model, the system was migrated to their recommended replacement, openai/gpt-oss-20b.

## Edge-TTS
Selected as a free text-to-speech solution suitable for the project's budget constraints. While not fully natural-sounding, it meets the functional requirements of the system.

## WebSockets
Used to provide real-time, persistent communication for visitor status updates, as a standard and appropriate choice for this requirement.

## Firebase Cloud Messaging (FCM)
Used to deliver push notifications to host employees' Android/iOS devices even when the app is backgrounded or closed. FCM is the standard, officially supported method for background/closed-app push delivery on both platforms, making it the appropriate choice for this requirement.

## Brevo (Transactional Email)
Initially, SendGrid was implemented as the standard industry option; however, its free tier was limited to 30 days. Brevo was subsequently selected as it offered a free tier (300 emails/day) with no credit card requirement and supported sender verification using a personal email address rather than requiring a custom domain. It has performed reliably in production, with no deliverability issues to date.

## ngrok
Used during development and demos to expose the locally hosted backend to external devices (tablet UI, host app, QR code scans), since running the full system on a single machine was not feasible in a multi-device demo setup. The stable ngrok session URL allowed the frontend to point to the backend consistently throughout testing.

## Testing Tools — Swagger UI and Custom Python Scripts
Endpoints were tested primarily through Swagger UI (FastAPI's built-in interactive documentation) and custom Python scripts written to simulate the complete multi-step visitor flow end-to-end.
