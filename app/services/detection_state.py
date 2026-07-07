class DetectionState:
    def __init__(self):
        self.forward_start_time = None
        self.last_seen_forward_time = None

    def reset(self):
        self.forward_start_time = None
        self.last_seen_forward_time = None


# single shared instance for now (one tablet, one active visitor at a time)
detection_state = DetectionState()