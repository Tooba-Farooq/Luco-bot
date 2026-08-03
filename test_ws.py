# test_ws.py
import asyncio
import websockets
import json

async def listen():
    uri = "ws://127.0.0.1:8000/ws/status/<paste-status-token-here>"
    async with websockets.connect(uri) as ws:
        print("connected, listening...")
        async for message in ws:
            print(json.loads(message))

asyncio.run(listen())