forward_start_time = None
last_seen_forward_time = None
GRACE_PERIOD_SECONDS = 1.5  # tune this — how long a "look away" is tolerated before reset

@router.post("/detect", response_model=DetectionResponse)
async def detect(frame: UploadFile = File(...)):
    global forward_start_time, last_seen_forward_time
    image_bytes = await frame.read()

    face_found, face_box = check_face_present(image_bytes)
    if not face_found:
        forward_start_time = None
        last_seen_forward_time = None
        return DetectionResponse(status="idle")

    is_forward = check_face_forward(image_bytes, face_box)
    now = time.time()

    if is_forward:
        if forward_start_time is None:
            forward_start_time = now
        last_seen_forward_time = now
        duration = now - forward_start_time
    else:
        # not forward right now — but check if we're still within grace period
        if last_seen_forward_time is not None and (now - last_seen_forward_time) < GRACE_PERIOD_SECONDS:
            # still within grace window — don't reset, just don't accumulate new time either
            duration = last_seen_forward_time - forward_start_time
        else:
            # genuinely looked away too long — reset
            forward_start_time = None
            last_seen_forward_time = None
            duration = 0.0

    if duration < 3.0:
        return DetectionResponse(status="detecting", face_forward=is_forward, forward_duration=duration)

    name, confidence = run_face_recognition(image_bytes)
    if name:
        return DetectionResponse(status="known", visitor_name=name, confidence=confidence, face_forward=True, forward_duration=duration)
    else:
        return DetectionResponse(status="unknown", face_forward=True, forward_duration=duration)