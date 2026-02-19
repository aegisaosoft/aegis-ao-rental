import os
import numpy as np
import cv2
from PIL import Image, ImageFilter
from rembg import remove

INPUT_DIR = "cars_input"
OUTPUT_DIR = "cars_output"

# 2x3 landscape = 600x400
W, H = 600, 400

# How large the object should be (with padding, but as large as possible)
TARGET_WIDTH_FILL = 0.88   # 88% of canvas width
TARGET_HEIGHT_FILL = 0.82  # 82% of canvas height

# Ground line (where the wheels sit)
GROUND_Y = int(H * 0.80)   # 80% of height

# Shadow
SHADOW_OPACITY = 0.20
SHADOW_BLUR = 9
SHADOW_Y_OFFSET = 5

os.makedirs(OUTPUT_DIR, exist_ok=True)

def pil_to_np_rgba(pil_img: Image.Image) -> np.ndarray:
    return np.array(pil_img.convert("RGBA"))

def np_to_pil_rgba(arr: np.ndarray) -> Image.Image:
    return Image.fromarray(arr.astype(np.uint8), "RGBA")

def tight_crop_rgba(rgba: np.ndarray, pad: int = 6) -> np.ndarray:
    alpha = rgba[:, :, 3]
    ys, xs = np.where(alpha > 8)
    if len(xs) == 0:
        return rgba
    x0, x1 = max(xs.min() - pad, 0), min(xs.max() + pad + 1, rgba.shape[1])
    y0, y1 = max(ys.min() - pad, 0), min(ys.max() + pad + 1, rgba.shape[0])
    return rgba[y0:y1, x0:x1]

def defringe(rgba: np.ndarray) -> np.ndarray:
    """
    Removes bright halos around edges after background removal:
    - slightly shrinks alpha
    - re-composites on white for clean edges (simple defringe)
    """
    rgb = rgba[:, :, :3].astype(np.float32)
    a = rgba[:, :, 3].astype(np.float32) / 255.0

    # Slightly erode the mask to remove fringe
    a8 = (a * 255).astype(np.uint8)
    k = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (3, 3))
    a8_er = cv2.erode(a8, k, iterations=1)
    a = a8_er.astype(np.float32) / 255.0

    # Re-composite on white for clean edges
    white = np.ones_like(rgb) * 255.0
    comp = rgb * a[..., None] + white * (1.0 - a[..., None])

    out = np.zeros_like(rgba)
    out[:, :, :3] = np.clip(comp, 0, 255).astype(np.uint8)
    out[:, :, 3] = (a * 255).astype(np.uint8)
    return out

def find_groundline(alpha: np.ndarray) -> int:
    """
    Finds the bottom point of the object (wheels/bottom) for alignment.
    """
    ys = np.where(alpha > 20)[0]
    if len(ys) == 0:
        return alpha.shape[0] - 1
    return int(ys.max())

def resize_to_target(rgba: np.ndarray) -> np.ndarray:
    h, w = rgba.shape[:2]
    # target dimensions (no crop)
    target_w = int(W * TARGET_WIDTH_FILL)
    target_h = int(H * TARGET_HEIGHT_FILL)
    scale = min(target_w / w, target_h / h)

    new_w = max(1, int(w * scale))
    new_h = max(1, int(h * scale))

    pil = np_to_pil_rgba(rgba)
    pil = pil.resize((new_w, new_h), Image.LANCZOS)
    return pil_to_np_rgba(pil)

def make_ground_shadow(alpha: np.ndarray) -> Image.Image:
    """
    Soft studio shadow under the entire car
    (not just under the wheels)
    """

    h, w = alpha.shape

    # Get object silhouette
    shadow = Image.fromarray(alpha, "L")

    # Squash vertically to create a flat shadow
    shadow = shadow.resize((w, int(h * 0.35)), Image.BILINEAR)

    # Heavy blur for softness
    shadow = shadow.filter(ImageFilter.GaussianBlur(18))

    # Create RGBA layer
    shadow_rgba = Image.new("RGBA", (w, int(h * 0.35)), (0, 0, 0, 0))

    # Light transparency
    shadow_rgba.putalpha(
        shadow.point(lambda p: int(p * 0.25))
    )

    return shadow_rgba

def process_file(path: str) -> Image.Image:
    # 1) remove background locally via rembg
    inp = Image.open(path).convert("RGBA")
    cut = remove(inp)  # RGBA with transparency

    rgba = pil_to_np_rgba(cut)
    rgba = tight_crop_rgba(rgba, pad=8)
    rgba = defringe(rgba)

    # 2) uniform scale
    rgba = resize_to_target(rgba)

    # 3) align to ground line
    alpha = rgba[:, :, 3]
    bottom = find_groundline(alpha)  # bottom of object in this crop

    obj_h, obj_w = rgba.shape[:2]
    x = (W - obj_w) // 2
    y = GROUND_Y - bottom  # place object bottom on ground line

    # 4) white canvas
    canvas = Image.new("RGBA", (W, H), (255, 255, 255, 255))

    # 5) shadow under the car
    shadow_layer = make_ground_shadow(alpha)
    shadow_y = y + int(obj_h * 0.65)
    canvas.alpha_composite(shadow_layer, (x, shadow_y))

    # 6) paste the car
    obj_pil = np_to_pil_rgba(rgba)
    canvas.alpha_composite(obj_pil, (x, y))

    return canvas

def main():
    files = [f for f in os.listdir(INPUT_DIR) if f.lower().endswith((".png", ".jpg", ".jpeg", ".webp"))]
    if not files:
        print("No files in cars_input")
        return

    for f in files:
        in_path = os.path.join(INPUT_DIR, f)
        out_name = os.path.splitext(f)[0] + ".png"
        out_path = os.path.join(OUTPUT_DIR, out_name)

        try:
            result = process_file(in_path)
            result.save(out_path, "PNG")
            print("OK:", out_name)
        except Exception as e:
            print("FAIL:", f, "->", e)

    print("DONE. Files in:", OUTPUT_DIR)

if __name__ == "__main__":
    main()
