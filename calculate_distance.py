from app.services.embedding_service import generate_face_embedding
from app.services.detection_service import _cosine_distance

you_photo_1 = generate_face_embedding(r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260726_23_02_54_Pro.jpg")  # original photo
you_photo_2 = generate_face_embedding(r"D:\ReactDjango projects\Lucobot_backend\visitor_photos\2766445fb87c4d0bbfccf4558650fd73.jpg")   # different photo, same person
ayesha_photo_1 = generate_face_embedding(r"C:\Users\tooba\Downloads\WhatsApp Image 2026-07-23 at 12.37.42 PM (1).jpeg")  # different person
ayesha_photo_2 = generate_face_embedding(r"C:\Users\tooba\Downloads\WhatsApp Image 2026-07-23 at 11.43.07 AM (1).jpeg")  # different person
abba_photo_1 = generate_face_embedding(r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260726_23_16_39_Pro.jpg")  # different person
abba_photo_2 = generate_face_embedding(r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260726_22_14_21_Pro.jpg")  # different person
mother = generate_face_embedding(r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260726_22_20_06_Pro.jpg")  # different person
sister_1 = generate_face_embedding(r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260726_22_29_25_Pro.jpg")  # different person
furqan = generate_face_embedding(r"D:\ReactDjango projects\Lucobot_backend\visitor_photos\0b2b5efd03884f0e8737f29068bcd071.jpg")  # different person
sister_2 = generate_face_embedding(r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260726_23_33_15_Pro.jpg")  # different person


print("you vs you (different photo):", _cosine_distance(you_photo_1, you_photo_2))
print("you vs mother:", _cosine_distance(you_photo_1, mother))
print("you vs sister_1:", _cosine_distance(you_photo_1, sister_1))
print("sister vs sister:", _cosine_distance(sister_1, sister_2))
print("you vs furqan:", _cosine_distance(you_photo_1, furqan))
print("ayesha vs ayesha (different photo):", _cosine_distance(ayesha_photo_1, ayesha_photo_2))
print("abba vs abba:", _cosine_distance(abba_photo_1, abba_photo_2))
print("furqan vs sister_1:", _cosine_distance(furqan, sister_1))
print("furqan vs sister_2:", _cosine_distance(furqan, sister_2))