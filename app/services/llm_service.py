from groq import AsyncGroq
import os
import json

client = AsyncGroq(api_key=os.getenv("GROQ_API_KEY"))
LLM_MODEL = "openai/gpt-oss-20b"

INTENT_PROMPT = """You are classifying a visitor's response at a reception desk.

The visitor was asked "How may I help you?" and responded with the text below.
This text came from speech-to-text, so it may be garbled, empty, or nonsensical
if the microphone picked up noise/silence instead of real speech.

Classify their intent as exactly one of:
- MEET_SOMEONE: they want to see/meet/ask about a specific person (even indirectly,
  e.g. asking if someone is available, present, free, or in today)
- GENERAL_QUERY: they're asking a coherent question or making a coherent request,
  unrelated to meeting a specific person
- UNCLEAR: the text is empty, gibberish, a stray word/fragment, or otherwise does
  not form a real, coherent request a human could act on (likely a transcription
  error, background noise, or the visitor trailing off)

If MEET_SOMEONE, also extract the person's name if mentioned (or null if unclear).

Respond ONLY with valid JSON, no other text:
{{"intent": "MEET_SOMEONE" or "GENERAL_QUERY" or "UNCLEAR", "person_name": "<name or null>"}}

Visitor's response: "{text}"
"""


async def classify_intent(text: str) -> dict:
    response = await client.chat.completions.create(
        model=LLM_MODEL,
        messages=[{"role": "user", "content": INTENT_PROMPT.format(text=text)}],
        temperature=0.0,
        reasoning_effort="low",
        response_format={"type": "json_object"}
    )
    result = json.loads(response.choices[0].message.content)
    return result  # {"intent": "MEET_SOMEONE", "person_name": "Misbah"} or GENERAL_QUERY or UNCLEAR


KNOWLEDGE_TEXT = """
- Office hours are 9 AM to 6 PM, Monday to Friday.
- The washroom is on the 2nd floor, near the elevator.
- Visitor parking is available in the basement.
""".strip()  # edit this with your actual info

QUERY_PROMPT = """You are a reception assistant. Answer ONLY using the knowledge below.
If the answer isn't in it, respond with exactly: NO_MATCH

Knowledge:
{kb_text}

Visitor's question: "{question}"
"""


async def answer_query(question: str) -> str:
    response = await client.chat.completions.create(
        model=LLM_MODEL,
        messages=[{"role": "user", "content": QUERY_PROMPT.format(kb_text=KNOWLEDGE_TEXT, question=question)}],
        temperature=0.0
    )
    return response.choices[0].message.content.strip()

NAME_RESOLUTION_PROMPT = """A visitor at a reception desk was asked their name. Their speech was
transcribed twice — once forced to English, once forced to Urdu — by a speech-to-text model.
One or both transcriptions may be wrong, garbled, or a hallucinated sentence unrelated to a name
(a known failure mode on short audio clips).

Using both transcriptions together, determine your best guess at the visitor's actual name and
return it in Roman/Latin script, using standard spelling conventions for Pakistani/Muslim names
(e.g. "Muhammad" not "Mohammad", "Abdul Rahman" not "Abd al-Rahman").

You must always return your best guess at a name, even if you are not fully confident — the visitor
will see this on screen and can edit it before confirming, so a reasonable guess is always more useful
than refusing to answer. Only if BOTH transcriptions are completely unusable (e.g. totally empty) should
you fall back to a generic placeholder.

English-forced transcription: "{en_text}"
Urdu-forced transcription: "{ur_text}"

Respond ONLY with valid JSON:
{{"name": "<your best-guess name in Roman script>"}}
"""


async def resolve_name(en_text: str, ur_text: str) -> str:
    response = await client.chat.completions.create(
        model=LLM_MODEL,
        messages=[{"role": "user", "content": NAME_RESOLUTION_PROMPT.format(en_text=en_text, ur_text=ur_text)}],
        temperature=0.0,
        reasoning_effort="low",
        response_format={"type": "json_object"}
    )
    result = json.loads(response.choices[0].message.content)
    name = result.get("name", "").strip()
    return name if name else "Guest"