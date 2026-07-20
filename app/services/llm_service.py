from groq import AsyncGroq
import os
import json

client = AsyncGroq(api_key=os.getenv("GROQ_API_KEY"))
LLM_MODEL = "llama-3.1-8b-instant"

INTENT_PROMPT = """You are classifying a visitor's response at a reception desk.

The visitor was asked "How may I help you?" and responded with the text below.
Classify their intent as exactly one of:
- MEET_SOMEONE: they want to see/meet/ask about a specific person (even indirectly,
  e.g. asking if someone is available, present, free, or in today)
- GENERAL_QUERY: they're asking a general question unrelated to meeting a specific person

If MEET_SOMEONE, also extract the person's name if mentioned (or null if unclear).

Respond ONLY with valid JSON, no other text:
{{"intent": "MEET_SOMEONE" or "GENERAL_QUERY", "person_name": "<name or null>"}}

Visitor's response: "{text}"
"""


async def classify_intent(text: str) -> dict:
    response = await client.chat.completions.create(
        model=LLM_MODEL,
        messages=[{"role": "user", "content": INTENT_PROMPT.format(text=text)}],
        temperature=0.0,
        response_format={"type": "json_object"}
    )
    result = json.loads(response.choices[0].message.content)
    return result  # {"intent": "MEET_SOMEONE", "person_name": "Misbah"} or GENERAL_QUERY


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