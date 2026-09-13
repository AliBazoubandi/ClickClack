import os
from PIL import Image
import numpy as np
from scipy import ndimage

def extract_clean_sprite(input_path, output_path, is_typewriter=True):
    img = Image.open(input_path).convert('RGBA')
    arr = np.array(img)
    h, w = arr.shape[:2]

    r = arr[:, :, 0].astype(int)
    g = arr[:, :, 1].astype(int)
    b = arr[:, :, 2].astype(int)

    # Background check: near-neutral grey checkerboard
    # In JPEG, grey squares have r,g,b very close to each other
    is_neutral = (np.abs(r - g) <= 22) & (np.abs(g - b) <= 22) & (np.abs(r - b) <= 22)
    is_in_range = (75 <= r) & (r <= 190) & (75 <= g) & (g <= 190) & (75 <= b) & (b <= 190)
    
    # Bottom text region (below 76% of image height)
    is_text_zone = np.zeros((h, w), dtype=bool)
    is_text_zone[int(h * 0.76):, :] = True

    is_bg = (is_neutral & is_in_range) | is_text_zone

    # Foreground
    fg = ~is_bg

    # Label connected components to filter out small noise specks
    labeled, num_features = ndimage.label(fg)
    if num_features > 0:
        sizes = ndimage.sum(fg, labeled, range(num_features + 1))
        # Keep components with size > 1200 pixels
        mask = sizes > 1200
        clean_fg = mask[labeled]
        # Fill any internal small holes inside the character/typewriter
        clean_fg = ndimage.binary_fill_holes(clean_fg)
    else:
        clean_fg = fg

    arr[~clean_fg, 3] = 0
    clean_img = Image.fromarray(arr)
    bbox = clean_img.getbbox()
    if bbox:
        cropped = clean_img.crop(bbox)
    else:
        cropped = clean_img

    if not is_typewriter:
        # Normalize pet sprites to consistent 256x256 canvas with bottom baseline alignment
        cw, ch = cropped.size
        # Group 2 (lower-res exports in assets: wave, eating, etc.) scaled so Inky's body matches Group 1
        group2_names = {
            'pet - eating.jpg', 'pet - eating2.jpg', 'pet - reading.jpg', 'pet - screen.jpg', 
            'pet - shocked.jpg', 'pet - sneak.jpg', 'pet - thinking.jpg', 'pet - wave.jpg'
        }
        target_body = 200.0
        scale_g1 = target_body / 446.0
        scale_g2 = target_body / 352.0

        src_base = os.path.basename(input_path)
        scale = scale_g2 if src_base in group2_names else scale_g1

        nw = int(round(cw * scale))
        nh = int(round(ch * scale))
        resample = getattr(Image, 'Resampling', Image).LANCZOS
        scaled = cropped.resize((nw, nh), resample=resample)

        CANVAS_W, CANVAS_H = 256, 256
        BOTTOM_MARGIN = 16
        canvas = Image.new('RGBA', (CANVAS_W, CANVAS_H), (0, 0, 0, 0))
        x = (CANVAS_W - nw) // 2
        y = CANVAS_H - BOTTOM_MARGIN - nh
        canvas.paste(scaled, (x, y), scaled)
        final_img = canvas
    else:
        final_img = cropped

    final_img.save(output_path, format="PNG")
    print(f"Extracted {os.path.basename(input_path)} -> {output_path} (size={final_img.size})")
    return final_img

def main():
    src_dir = r"e:\task manager\assets"
    dest_tw = r"e:\task manager\PixelCompanion\Assets\Typewriter"
    dest_pet = r"e:\task manager\PixelCompanion\Assets\Character"
    dest_icon = r"e:\task manager\PixelCompanion\Assets\Icons"

    os.makedirs(dest_tw, exist_ok=True)
    os.makedirs(dest_pet, exist_ok=True)
    os.makedirs(dest_icon, exist_ok=True)

    # 1. Typewriter Assets Mapping
    tw_mapping = {
        "frame 1 - IDLE.jpg": "idle.png",
        "frame 2 - PAPER STARTING.jpg": "paper_starting.png",
        "frame 3 - PAPER EXTENTED.jpg": "paper_extended.png",
        "frame 4 - PRESS.jpg": "press.png",
        "frame 5 - PAPER RETRACTING.jpg": "paper_retracting.png",
        "STANDALONE TRANSPARENT.jpg": "paper_sheet.png"
    }

    tw_images = {}
    for src_name, dest_name in tw_mapping.items():
        src_path = os.path.join(src_dir, src_name)
        dest_path = os.path.join(dest_tw, dest_name)
        if os.path.exists(src_path):
            tw_images[dest_name] = extract_clean_sprite(src_path, dest_path, is_typewriter=True)

    # 2. Pet Assets Mapping (all poses, normalized to 256x256, diging -> digging)
    pet_mapping = {
        "pet - idle.jpg": "idle.png",
        "pet - idle2.jpg": "idle2.png",
        "pet - celebrate.jpg": "celebrate.png",
        "pet - curious.jpg": "curious.png",
        "pet - diging.jpg": "digging.png",
        "pet - eating.jpg": "eating.png",
        "pet - eating2.jpg": "eating2.png",
        "pet - flower.jpg": "flower.png",
        "pet - happy.jpg": "happy.png",
        "pet - hiding.jpg": "hiding.png",
        "pet - reading.jpg": "reading.png",
        "pet - screen.jpg": "screen.png",
        "pet - shocked.jpg": "shocked.png",
        "pet - sleep.jpg": "sleep.png",
        "pet - sneak.jpg": "sneak.png",
        "pet - studying.jpg": "studying.png",
        "pet - teatime.jpg": "teatime.png",
        "pet - thinking.jpg": "thinking.png",
        "pet - wave.jpg": "wave.png"
    }

    pet_images = {}
    for src_name, dest_name in pet_mapping.items():
        src_path = os.path.join(src_dir, src_name)
        dest_path = os.path.join(dest_pet, dest_name)
        if os.path.exists(src_path):
            pet_images[dest_name] = extract_clean_sprite(src_path, dest_path, is_typewriter=False)

    # 3. Create updated app.ico with real artwork from icon-simple.jpg
    icon_simple_path = os.path.join(src_dir, "icon-simple.jpg")
    if os.path.exists(icon_simple_path):
        icon_img = Image.open(icon_simple_path).convert("RGBA")
        icon_path = os.path.join(dest_icon, "app.ico")
        icon_sizes = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
        icon_img.save(icon_path, format="ICO", sizes=icon_sizes)
        print(f"Generated new App Icon from icon-simple.jpg: {icon_path}")

if __name__ == "__main__":
    main()
