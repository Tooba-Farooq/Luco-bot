import uuid

class DetectionState:
    def __init__(self):
        self.forward_start_time = None
        self.last_seen_forward_time = None
        self.session_id = None
        self.state = "IDLE"
        self.visitor_id = None
        self.visit_log_id = None
        # meet-someone flow
        self.intent = None
        self.host_candidates = None
        self.selected_host_id = None
        self.purpose = None
        self.recognized_name = None
        # name/photo, now captured at the end
        self.heard_name = None
        self.detected_lang = None
        # general query flow
        self.last_query_answer = None
        self.photo_steady_start_time = None
        

    def reset(self):
        self.__init__()

    def start_session(self):
        self.session_id = str(uuid.uuid4())
        self.state = "AWAITING_INTENT"
        return self.session_id
    

# single shared instance for now (one tablet, one active visitor at a time)
detection_state = DetectionState()