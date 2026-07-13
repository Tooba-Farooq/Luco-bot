import edge_tts
import asyncio

urdu_uzma= "ur-PK-UzmaNeural"
urdu_asad = "ur-PK-AsadNeural"
eng_jenny = "en-US-JennyNeural"
multi_andrew = "en-US-AndrewMultilingualNeural"
multi_ava = "en-US-AvaMultilingualNeural"
multi_emma = "en-US-EmmaMultilingualNeural"
hindi_male = "hi-IN-MadhurNeural"

async def generate_speech(text: str, output_path: str, lang: str = "ur"):
    voice = multi_emma if lang == "en" else hindi_male
    communicate = edge_tts.Communicate(text, voice)
    await communicate.save(output_path)

# usage
asyncio.run(generate_speech("Hello Ahmed, how may I help you today?", "greeting.mp3"))

