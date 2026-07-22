using System;

[Serializable]
public class HostCandidate
{
    public int id;
    public string name;
}

[Serializable]
public class SessionResponse
{
    public string session_id;
    public string state;
    public string heard_text;
    public string detected_lang;
    public string greeting_text;
    public string answer_text;
    public HostCandidate matched_host;
    public HostCandidate[] host_candidates;
    public string audio_base64;
    public string audio_key;
}