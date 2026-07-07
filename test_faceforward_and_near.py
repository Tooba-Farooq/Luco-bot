from app.services.detection_service import check_face_present, check_face_forward

test_images = [
    r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260706_12_23_22_Pro.jpg",  # add a mix: straight-on, angled, far away
    r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260706_12_23_34_Pro.jpg",
    r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260706_12_23_44_Pro.jpg",
    r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260706_13_02_09_Pro.jpg",
    r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260706_13_05_17_Pro.jpg",
    r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260706_13_05_26_Pro.jpg",
    r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260707_10_06_12_Pro.jpg",
    r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260707_10_06_18_Pro.jpg",
    r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260707_10_06_24_Pro.jpg",
    r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260707_10_06_28_Pro.jpg",
    r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260707_10_07_25_Pro.jpg",
    
]

for img_path in test_images:
    found, box = check_face_present(img_path)
    if not found:
        print(f"{img_path} -> No face found")
        continue
    is_forward = check_face_forward(img_path, box, debug=True)
    print(f"{img_path} -> Forward: {is_forward}")