import os
from PIL import Image, ImageDraw

def create_pixel_assets():
    base_dir = os.path.dirname(os.path.abspath(__file__))
    tw_dir = os.path.join(base_dir, "PixelCompanion", "Assets", "Typewriter")
    char_dir = os.path.join(base_dir, "PixelCompanion", "Assets", "Character")
    icon_dir = os.path.join(base_dir, "PixelCompanion", "Assets", "Icons")
    
    os.makedirs(tw_dir, exist_ok=True)
    os.makedirs(char_dir, exist_ok=True)
    os.makedirs(icon_dir, exist_ok=True)
    
    SCALE = 4 # Each pixel art unit becomes 4x4 crisp screen pixels
    
    # -------------------------------------------------------------
    # 1. TYPEWRITER (Native grid: 56 wide x 44 high)
    # -------------------------------------------------------------
    # Palette
    C_TRANS = (0, 0, 0, 0)
    C_OUTLINE = (28, 32, 38, 255)
    C_BODY_DARK = (38, 68, 72, 255)      # Deep vintage teal
    C_BODY_MID = (54, 94, 98, 255)       # Main vintage teal
    C_BODY_LIGHT = (76, 125, 130, 255)   # Teal highlight
    C_BODY_SHINE = (112, 168, 172, 255)  # Specular edge
    
    C_GOLD_DARK = (130, 88, 28, 255)
    C_GOLD_MID = (195, 142, 42, 255)
    C_GOLD_LIGHT = (245, 202, 98, 255)
    C_GOLD_SHINE = (255, 240, 180, 255)
    
    C_METAL_DARK = (52, 58, 66, 255)
    C_METAL_MID = (98, 108, 120, 255)
    C_METAL_LIGHT = (168, 178, 192, 255)
    C_METAL_SHINE = (230, 238, 248, 255)
    
    C_PLATEN_DARK = (24, 25, 30, 255)
    C_PLATEN_MID = (46, 48, 56, 255)
    
    C_PAPER_SHADOW = (205, 192, 170, 255)
    C_PAPER_MID = (238, 230, 212, 255)
    C_PAPER_LIGHT = (252, 248, 238, 255)
    C_PAPER_LINE = (185, 170, 145, 255)
    
    C_KEY_RIM = (190, 198, 206, 255)
    C_KEY_FACE = (45, 48, 56, 255)
    C_KEY_PRESS = (75, 80, 92, 255)
    C_KEY_TEXT = (220, 225, 230, 255)
    
    C_RIBBON_RED = (180, 42, 42, 255)
    C_RIBBON_BLACK = (32, 32, 36, 255)
    
    C_SPARK = (255, 235, 140, 255)
    C_SPARK_CORE = (255, 255, 255, 255)

    def draw_typewriter_base(img, paper_offset_y=0, keys_pressed=False, hammer_up=False, spark=False):
        d = ImageDraw.Draw(img)
        
        # --- CARRIAGE & PAPER (Behind typewriter body) ---
        # Carriage roller bar
        # Platen (roller cylinder) x: 10 to 45, y: 10 to 14
        d.rectangle([10, 10 - paper_offset_y, 45, 14 - paper_offset_y], fill=C_PLATEN_DARK, outline=C_OUTLINE)
        d.line([11, 11 - paper_offset_y, 44, 11 - paper_offset_y], fill=C_PLATEN_MID)
        
        # Platen side knobs (gold/metal knobs on left and right of roller)
        d.rectangle([7, 10 - paper_offset_y, 9, 14 - paper_offset_y], fill=C_GOLD_MID, outline=C_OUTLINE)
        d.point([(8, 11 - paper_offset_y)], fill=C_GOLD_LIGHT)
        d.rectangle([46, 10 - paper_offset_y, 48, 14 - paper_offset_y], fill=C_GOLD_MID, outline=C_OUTLINE)
        d.point([(47, 11 - paper_offset_y)], fill=C_GOLD_LIGHT)
        
        # Carriage return lever (classic silver sweep on left)
        d.line([8, 10 - paper_offset_y, 5, 7 - paper_offset_y], fill=C_OUTLINE)
        d.line([5, 7 - paper_offset_y, 5, 4 - paper_offset_y], fill=C_METAL_LIGHT)
        d.line([4, 4 - paper_offset_y, 8, 4 - paper_offset_y], fill=C_METAL_SHINE)
        d.point([(5, 4 - paper_offset_y)], fill=C_METAL_SHINE)
        
        # Paper sheet sticking out of platen
        # y: 2 to 10
        paper_top = 2 - paper_offset_y
        d.rectangle([14, paper_top, 41, 10 - paper_offset_y], fill=C_PAPER_MID, outline=C_OUTLINE)
        d.line([15, paper_top + 1, 40, paper_top + 1], fill=C_PAPER_LIGHT)
        # Ruled lines on paper
        d.line([17, paper_top + 3, 38, paper_top + 3], fill=C_PAPER_LINE)
        d.line([17, paper_top + 5, 34, paper_top + 5], fill=C_PAPER_LINE)
        if paper_top <= 1:
            d.line([17, paper_top + 7, 30, paper_top + 7], fill=C_PAPER_LINE)
            
        # Paper bail (metal wire clip holding paper against roller)
        d.line([12, 13 - paper_offset_y, 43, 13 - paper_offset_y], fill=C_METAL_LIGHT)
        d.point([(18, 13 - paper_offset_y), (37, 13 - paper_offset_y)], fill=C_GOLD_LIGHT)
        
        # --- MAIN BODY ---
        # Upper housing (sloped trapezoid body)
        # Outline
        body_poly = [
            (8, 15), (47, 15),
            (51, 26), (51, 38),
            (4, 38), (4, 26)
        ]
        d.polygon(body_poly, fill=C_BODY_MID, outline=C_OUTLINE)
        # Shading
        d.polygon([(9, 16), (46, 16), (48, 22), (7, 22)], fill=C_BODY_LIGHT)
        d.line([(10, 16), (45, 16)], fill=C_BODY_SHINE)
        d.rectangle([5, 30, 50, 37], fill=C_BODY_DARK)
        d.line([5, 30, 50, 30], fill=C_BODY_MID)
        
        # Rubber feet under typewriter
        d.rectangle([6, 38, 10, 40], fill=C_PLATEN_DARK, outline=C_OUTLINE)
        d.rectangle([45, 38, 49, 40], fill=C_PLATEN_DARK, outline=C_OUTLINE)
        
        # --- TYPEBAR BASKET & RIBBON (center cutout) ---
        # Center segment cutout
        d.polygon([(18, 17), (37, 17), (34, 24), (21, 24)], fill=C_METAL_DARK, outline=C_OUTLINE)
        # Typebars (radiating metal strike bars)
        d.line([(21, 18), (26, 23)], fill=C_METAL_MID)
        d.line([(24, 18), (27, 23)], fill=C_METAL_MID)
        d.line([(27, 18), (28, 23)], fill=C_METAL_LIGHT)
        d.line([(31, 18), (28, 23)], fill=C_METAL_MID)
        d.line([(34, 18), (29, 23)], fill=C_METAL_MID)
        
        # Ribbon guide & vibrator
        d.line([25, 16, 25, 19], fill=C_RIBBON_RED)
        d.line([26, 16, 26, 19], fill=C_RIBBON_BLACK)
        d.line([29, 16, 29, 19], fill=C_RIBBON_BLACK)
        d.line([30, 16, 30, 19], fill=C_RIBBON_RED)
        
        # Ribbon spools (circular covered spools on sides)
        d.ellipse([11, 18, 17, 24], fill=C_METAL_DARK, outline=C_OUTLINE)
        d.point([(14, 21)], fill=C_GOLD_MID)
        d.ellipse([38, 18, 44, 24], fill=C_METAL_DARK, outline=C_OUTLINE)
        d.point([(41, 21)], fill=C_GOLD_MID)
        
        # Vintage gold logo badge in center of front slope
        d.rectangle([23, 26, 32, 28], fill=C_GOLD_MID, outline=C_GOLD_DARK)
        d.line([24, 27, 31, 27], fill=C_GOLD_SHINE)

        # Hammer animation
        if hammer_up:
            d.line([27, 22, 27, 14], fill=C_METAL_SHINE)
            d.rectangle([26, 12, 28, 14], fill=C_METAL_SHINE, outline=C_OUTLINE)

        # Spark / mechanical strike star
        if spark:
            d.point([(27, 11), (26, 12), (28, 12), (27, 13)], fill=C_SPARK_CORE)
            d.point([(27, 10), (27, 14), (25, 12), (29, 12)], fill=C_SPARK)
            d.point([(25, 10), (29, 10), (25, 14), (29, 14)], fill=C_GOLD_LIGHT)

        # --- KEYBOARD ---
        # 3 Rows of round keys + space bar
        key_offset_y = 1 if keys_pressed else 0
        
        # Row 1 (upper keys): 7 keys
        r1_y = 31 + key_offset_y
        for x in [8, 13, 18, 23, 28, 33, 38, 43]:
            d.rectangle([x, r1_y, x + 3, r1_y + 2], fill=C_KEY_RIM, outline=C_OUTLINE)
            d.point([(x + 1, r1_y + 1)], fill=C_KEY_PRESS if keys_pressed else C_KEY_FACE)

        # Row 2 (middle keys): 7 keys slightly staggered
        r2_y = 33 + key_offset_y
        for x in [10, 15, 20, 25, 30, 35, 40]:
            d.rectangle([x, r2_y, x + 3, r2_y + 2], fill=C_KEY_RIM, outline=C_OUTLINE)
            d.point([(x + 1, r2_y + 1)], fill=C_KEY_PRESS if keys_pressed else C_KEY_FACE)

        # Space bar
        sb_y = 35 + key_offset_y
        d.rectangle([17, sb_y, 38, sb_y + 2], fill=C_KEY_RIM, outline=C_OUTLINE)
        d.line([18, sb_y + 1, 37, sb_y + 1], fill=C_KEY_PRESS if keys_pressed else C_KEY_FACE)

    # -------------------------------------------------------------
    # 2. COMPANION (Meet "Pip", cozy ink-kitten desktop pet)
    # Native grid: 36 wide x 36 high
    # -------------------------------------------------------------
    C_PET_OUTLINE = (22, 24, 34, 255)
    C_PET_DARK = (44, 46, 62, 255)        # Soft charcoal body
    C_PET_MID = (65, 68, 90, 255)         # Main coat
    C_PET_LIGHT = (88, 92, 118, 255)      # Fluff highlight
    C_PET_SHINE = (120, 125, 155, 255)
    
    C_PET_BELLY = (248, 242, 230, 255)    # Cream snout/belly
    C_PET_BELLY_SHADOW = (222, 212, 195, 255)
    C_EAR_INNER = (255, 175, 175, 255)    # Soft pink inner ear
    C_BLUSH = (255, 130, 150, 255)        # Cheerful cheeks
    
    C_EYE_DARK = (16, 18, 26, 255)
    C_EYE_GLINT = (255, 255, 255, 255)
    C_GOLD_STAR = (255, 215, 65, 255)

    def draw_companion(img, state="idle_1"):
        d = ImageDraw.Draw(img)
        # Breathing / bob offset
        bob_y = 1 if state == "idle_2" else 0
        celebrate = (state == "celebrate")
        blink = (state == "blink")
        
        y_off = -2 if celebrate else bob_y

        # --- TAIL (curled behind) ---
        tail_tip_y = 20 + y_off if not celebrate else 14 + y_off
        d.line([6, 26 + y_off, 4, 23 + y_off], fill=C_PET_DARK, width=2)
        d.line([4, 23 + y_off, 5, tail_tip_y], fill=C_PET_MID, width=2)
        d.point([(5, tail_tip_y), (6, tail_tip_y)], fill=C_PET_LIGHT)
        d.point([(4, tail_tip_y - 1)], fill=C_PET_OUTLINE)

        # --- BODY & PAWS ---
        # Main body plump bean shape
        d.ellipse([7, 14 + y_off, 28, 33 + y_off], fill=C_PET_MID, outline=C_PET_OUTLINE)
        # Highlights on top of head
        d.line([13, 15 + y_off, 22, 15 + y_off], fill=C_PET_LIGHT)
        d.line([14, 16 + y_off, 21, 16 + y_off], fill=C_PET_SHINE)

        # --- EARS ---
        if celebrate:
            # Perky excited ears
            d.polygon([(9, 14 + y_off), (6, 7 + y_off), (13, 12 + y_off)], fill=C_PET_MID, outline=C_PET_OUTLINE)
            d.polygon([(8, 12 + y_off), (7, 9 + y_off), (11, 12 + y_off)], fill=C_EAR_INNER)
            
            d.polygon([(26, 14 + y_off), (29, 7 + y_off), (22, 12 + y_off)], fill=C_PET_MID, outline=C_PET_OUTLINE)
            d.polygon([(27, 12 + y_off), (28, 9 + y_off), (24, 12 + y_off)], fill=C_EAR_INNER)
        else:
            # Cute round cat ears
            d.polygon([(9, 16 + y_off), (8, 10 + y_off), (14, 14 + y_off)], fill=C_PET_MID, outline=C_PET_OUTLINE)
            d.point([(9, 12 + y_off), (10, 13 + y_off)], fill=C_EAR_INNER)
            
            d.polygon([(26, 16 + y_off), (27, 10 + y_off), (21, 14 + y_off)], fill=C_PET_MID, outline=C_PET_OUTLINE)
            d.point([(26, 12 + y_off), (25, 13 + y_off)], fill=C_EAR_INNER)

        # Tiny beret / quill decoration on right ear
        d.ellipse([20, 11 + y_off, 26, 14 + y_off], fill=C_GOLD_MID, outline=C_GOLD_DARK)
        d.point([(23, 10 + y_off)], fill=C_GOLD_LIGHT)

        # --- CREAM BELLY & SNOUT ---
        d.ellipse([11, 21 + y_off, 24, 31 + y_off], fill=C_PET_BELLY, outline=C_PET_OUTLINE)
        d.line([12, 29 + y_off, 23, 29 + y_off], fill=C_PET_BELLY_SHADOW)

        # --- NOSE & MOUTH ---
        # Tiny pink nose
        d.point([(17, 21 + y_off), (18, 21 + y_off)], fill=C_EAR_INNER)
        # Cute :3 mouth
        d.point([(16, 22 + y_off), (19, 22 + y_off)], fill=C_PET_OUTLINE)

        # --- BLUSH CHEEKS ---
        d.rectangle([10, 22 + y_off, 12, 23 + y_off], fill=C_BLUSH)
        d.rectangle([23, 22 + y_off, 25, 23 + y_off], fill=C_BLUSH)

        # --- EYES ---
        if blink:
            # Soft happy curved lines ^ ^
            d.line([(12, 19 + y_off), (14, 18 + y_off)], fill=C_EYE_DARK, width=1)
            d.line([(14, 18 + y_off), (16, 19 + y_off)], fill=C_EYE_DARK, width=1)
            d.line([(19, 19 + y_off), (21, 18 + y_off)], fill=C_EYE_DARK, width=1)
            d.line([(21, 18 + y_off), (23, 19 + y_off)], fill=C_EYE_DARK, width=1)
        elif celebrate:
            # Joyful stars or shining eyes > <
            d.line([(12, 17 + y_off), (15, 19 + y_off)], fill=C_EYE_DARK)
            d.line([(12, 20 + y_off), (15, 18 + y_off)], fill=C_EYE_DARK)
            d.line([(20, 18 + y_off), (23, 20 + y_off)], fill=C_EYE_DARK)
            d.line([(20, 19 + y_off), (23, 17 + y_off)], fill=C_EYE_DARK)
            # Joy sparkles around companion
            d.point([(4, 11), (5, 12), (3, 12), (4, 13)], fill=C_GOLD_STAR)
            d.point([(31, 9), (32, 10), (30, 10), (31, 11)], fill=C_GOLD_STAR)
            d.point([(29, 24), (30, 24)], fill=C_GOLD_STAR)
        else:
            # Big curious anime pixel eyes (idle 1 & 2)
            d.rectangle([12, 17 + y_off, 15, 20 + y_off], fill=C_EYE_DARK)
            d.rectangle([20, 17 + y_off, 23, 20 + y_off], fill=C_EYE_DARK)
            # Big white specular glint
            d.point([(13, 17 + y_off), (21, 17 + y_off)], fill=C_EYE_GLINT)
            d.point([(14, 19 + y_off), (22, 19 + y_off)], fill=(130, 145, 175, 255))

        # --- PAWS ---
        if celebrate:
            # Paws raised up in air!
            d.ellipse([8, 14 + y_off, 12, 18 + y_off], fill=C_PET_BELLY, outline=C_PET_OUTLINE)
            d.ellipse([23, 14 + y_off, 27, 18 + y_off], fill=C_PET_BELLY, outline=C_PET_OUTLINE)
        else:
            # Tiny cute paws resting forward
            d.ellipse([11, 26 + y_off, 15, 29 + y_off], fill=C_PET_BELLY, outline=C_PET_OUTLINE)
            d.ellipse([20, 26 + y_off, 24, 29 + y_off], fill=C_PET_BELLY, outline=C_PET_OUTLINE)

        # Sitting feet at bottom
        d.ellipse([8, 30 + y_off, 13, 33 + y_off], fill=C_PET_MID, outline=C_PET_OUTLINE)
        d.ellipse([22, 30 + y_off, 27, 33 + y_off], fill=C_PET_MID, outline=C_PET_OUTLINE)

    # -------------------------------------------------------------
    # Render and scale helper
    # -------------------------------------------------------------
    def save_scaled(img, path, scale=SCALE):
        scaled = img.resize((img.width * scale, img.height * scale), Image.NEAREST)
        scaled.save(path, format="PNG")
        print(f"Generated: {path} ({scaled.width}x{scaled.height})")

    # Generate Typewriter frames (Native 56x44 -> 224x176 at 4x)
    tw_idle = Image.new("RGBA", (56, 44), C_TRANS)
    draw_typewriter_base(tw_idle, paper_offset_y=0, keys_pressed=False, hammer_up=False, spark=False)
    save_scaled(tw_idle, os.path.join(tw_dir, "idle.png"))

    tw_press_1 = Image.new("RGBA", (56, 44), C_TRANS)
    draw_typewriter_base(tw_press_1, paper_offset_y=0, keys_pressed=True, hammer_up=True, spark=False)
    save_scaled(tw_press_1, os.path.join(tw_dir, "press_1.png"))

    tw_press_2 = Image.new("RGBA", (56, 44), C_TRANS)
    draw_typewriter_base(tw_press_2, paper_offset_y=2, keys_pressed=False, hammer_up=True, spark=True)
    save_scaled(tw_press_2, os.path.join(tw_dir, "press_2.png"))

    tw_active = Image.new("RGBA", (56, 44), C_TRANS)
    draw_typewriter_base(tw_active, paper_offset_y=2, keys_pressed=False, hammer_up=False, spark=False)
    save_scaled(tw_active, os.path.join(tw_dir, "active.png"))

    # Generate Character frames (Native 36x36 -> 144x144 at 4x)
    char_idle_1 = Image.new("RGBA", (36, 36), C_TRANS)
    draw_companion(char_idle_1, "idle_1")
    save_scaled(char_idle_1, os.path.join(char_dir, "idle_1.png"))

    char_idle_2 = Image.new("RGBA", (36, 36), C_TRANS)
    draw_companion(char_idle_2, "idle_2")
    save_scaled(char_idle_2, os.path.join(char_dir, "idle_2.png"))

    char_blink = Image.new("RGBA", (36, 36), C_TRANS)
    draw_companion(char_blink, "blink")
    save_scaled(char_blink, os.path.join(char_dir, "blink.png"))

    char_celebrate = Image.new("RGBA", (36, 36), C_TRANS)
    draw_companion(char_celebrate, "celebrate")
    save_scaled(char_celebrate, os.path.join(char_dir, "celebrate.png"))

    # Generate App Icon (Combination of Typewriter & Pet)
    icon_canvas = Image.new("RGBA", (64, 64), C_TRANS)
    # Paste mini typewriter and pet together on 64x64
    tw_cropped = tw_idle.crop((6, 4, 52, 42))
    pet_cropped = char_idle_1.crop((4, 6, 32, 34))
    
    icon_canvas.paste(tw_cropped, (4, 20), tw_cropped)
    icon_canvas.paste(pet_cropped, (34, 12), pet_cropped)
    
    icon_path_ico = os.path.join(icon_dir, "app.ico")
    # Save multi-size icon: 16, 32, 48, 64, 128, 256
    icon_sizes = [(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
    icon_imgs = []
    for s in icon_sizes:
        icon_imgs.append(icon_canvas.resize(s, Image.NEAREST))
    icon_imgs[0].save(icon_path_ico, format="ICO", sizes=icon_sizes)
    print(f"Generated App Icon: {icon_path_ico}")

if __name__ == "__main__":
    create_pixel_assets()
