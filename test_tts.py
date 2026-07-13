import edge_tts
import asyncio
import pygame

VOICES = {
    # "urdu_uzma": "ur-PK-UzmaNeural",
    # "urdu_asad": "ur-PK-AsadNeural",
    # "eng_jenny": "en-US-JennyNeural",
    # "multi_andrew": "en-US-AndrewMultilingualNeural",
    # "multi_ava": "en-US-AvaMultilingualNeural",
    "multi_emma": "en-US-EmmaMultilingualNeural",
    "hindi_male": "hi-IN-MadhurNeural",
    
    # "hindi_sawara": "hi-IN-SwaraNeural",
}

TEXT = "Hello Ahmed, Tooba, Ayesha, Misbah, Rudaina, Abeeha, Furqan, Muhammad, Eman how may I help you today? آپ کا نام کیا ہے؟ آپ سے مل کر خوشی ہوئی۔"

pygame.mixer.init()


async def generate_speech(text: str, voice: str, output_path: str):
    communicate = edge_tts.Communicate(text, voice)
    await communicate.save(output_path)


def play_and_wait(filepath: str):
    pygame.mixer.music.load(filepath)
    pygame.mixer.music.play()
    while pygame.mixer.music.get_busy():
        pygame.time.Clock().tick(10)
    pygame.mixer.music.unload()  # release the file so it can be reused/overwritten


async def run_all_voices():
    for label, voice in VOICES.items():
        print(f"\n=== Now playing: {label} ({voice}) ===")
        output_path = f"test_{label}.mp3"
        await generate_speech(TEXT, voice, output_path)
        play_and_wait(output_path)
        input("Press Enter for next voice...")


if __name__ == "__main__":
    asyncio.run(run_all_voices())