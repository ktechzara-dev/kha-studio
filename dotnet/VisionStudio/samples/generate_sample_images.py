import math
import os
import random
from PIL import Image, ImageDraw, ImageFont

OUTPUT_DIR = os.path.join(os.path.dirname(__file__), "images")
os.makedirs(OUTPUT_DIR, exist_ok=True)

WIDTH, HEIGHT = 800, 600

def get_font(size):
    try:
        return ImageFont.truetype("arial.ttf", size)
    except:
        return ImageFont.load_default()

# -------------------------------------------------------------
# 1. Stepped Shaft Dimension Measurement (Caliper & Dimensions)
# -------------------------------------------------------------
def generate_stepped_shaft():
    im = Image.new("RGB", (WIDTH, HEIGHT), (22, 26, 34))
    draw = ImageDraw.Draw(im)
    
    # Grid lines for inspection table
    for x in range(0, WIDTH, 50):
        draw.line([(x, 0), (x, HEIGHT)], fill=(30, 36, 48), width=1)
    for y in range(0, HEIGHT, 50):
        draw.line([(0, y), (WIDTH, y)], fill=(30, 36, 48), width=1)
        
    cy = 300
    
    # Section 1: D=120px (12.0mm), L=180px
    x1, x2 = 60, 240
    r1 = 60
    draw.rectangle([x1, cy - r1, x2, cy + r1], fill=(195, 205, 218), outline=(240, 245, 255), width=2)
    # Metallic gradient / highlight line
    draw.line([x1, cy - 25, x2, cy - 25], fill=(255, 255, 255), width=4)
    draw.line([x1, cy + 20, x2, cy + 20], fill=(140, 150, 165), width=3)
    
    # Section 2: D=240px (24.0mm), L=280px (Main Body with O-ring groove)
    x3, x4 = 240, 520
    r2 = 120
    draw.rectangle([x3, cy - r2, x4, cy + r2], fill=(210, 218, 228), outline=(245, 250, 255), width=2)
    draw.line([x3, cy - 50, x4, cy - 50], fill=(255, 255, 255), width=6)
    draw.line([x3, cy + 45, x4, cy + 45], fill=(135, 145, 160), width=4)
    
    # O-ring Groove in middle section: width=36px, depth=24px each side
    gx1, gx2 = 360, 396
    draw.rectangle([gx1, cy - r2, gx2, cy - r2 + 24], fill=(90, 100, 115), outline=(50, 60, 70), width=1)
    draw.rectangle([gx1, cy + r2 - 24, gx2, cy + r2], fill=(90, 100, 115), outline=(50, 60, 70), width=1)
    
    # Section 3: D=160px (16.0mm), L=200px
    x5, x6 = 520, 720
    r3 = 80
    draw.rectangle([x5, cy - r3, x6, cy + r3], fill=(195, 205, 218), outline=(240, 245, 255), width=2)
    draw.line([x5, cy - 35, x6, cy - 35], fill=(255, 255, 255), width=4)
    draw.line([x5, cy + 30, x6, cy + 30], fill=(140, 150, 165), width=3)
    
    # Chamfer on right end
    draw.polygon([(x6 - 15, cy - r3), (x6, cy - r3 + 15), (x6, cy + r3 - 15), (x6 - 15, cy + r3)], fill=(170, 180, 195))
    
    # Centerline
    draw.line([30, cy, 760, cy], fill=(56, 189, 248, 160), width=1)
    
    # Annotations
    font_s = get_font(13)
    font_l = get_font(16)
    draw.text((20, 20), "PRECISION MACHINED STEPPED SHAFT (CALIPER INSPECTION)", fill=(56, 189, 248), font=font_l)
    draw.text((20, 44), "Target Measurements: Outer Dia D1=12.0mm, D2=24.0mm, D3=16.0mm | Groove Width=3.6mm", fill=(148, 163, 184), font=font_s)
    
    # Measurement target arrows
    draw.line([240, cy - r2 - 20, 520, cy - r2 - 20], fill=(16, 185, 129), width=2)
    draw.text((340, cy - r2 - 42), "L2 = 28.00 mm", fill=(16, 185, 129), font=font_s)

    im.save(os.path.join(OUTPUT_DIR, "01_stepped_shaft_dimension.png"))
    print("Generated 01_stepped_shaft_dimension.png")

# -------------------------------------------------------------
# 2. Industrial Codes & Labels (1D Barcode, DataMatrix, QR)
# -------------------------------------------------------------
def generate_industrial_codes():
    im = Image.new("RGB", (WIDTH, HEIGHT), (24, 28, 36))
    draw = ImageDraw.Draw(im)
    
    # White vinyl label card
    lx1, ly1, lx2, ly2 = 80, 60, 720, 540
    draw.rounded_rectangle([lx1, ly1, lx2, ly2], radius=14, fill=(245, 248, 252), outline=(200, 210, 225), width=3)
    
    font_h = get_font(20)
    font_m = get_font(14)
    font_s = get_font(11)
    
    # Header of label
    draw.text((110, 85), "INDUSTRIAL LOGISTICS & TRACEABILITY LABEL", fill=(15, 23, 42), font=font_h)
    draw.text((110, 115), "PART NO: KHA-VS-8902-X | REVISION: C03 | PASS ISO 15415/15416", fill=(71, 85, 105), font=font_m)
    draw.line([(110, 140), (690, 140)], fill=(203, 213, 225), width=2)
    
    # 1D Barcode (Code-128)
    bx, by, bw, bh = 110, 160, 4, 90
    bar_pattern = [1,0,1,1,0,0,1,1,0,1,0,0,1,1,0,1,1,1,0,1,0,1,1,0,0,1,1,0,1,0,1,1,1,0,0,1,0,1,1,0,1,1,0,0,1,0,1,1,1,0,1,0,0,1,1,0,1,1,0,1,0,1,1,1,0,1,1,0,0,1,1,0,1,0,1,1,0,0,1,1,0,1,0,0,1,1,0,1,1,1,0,1,0,1,1,0,0,1,1,0,1,0,1,1,1,0,0,1,0,1,1,0]
    cur_x = bx
    for b in bar_pattern:
        if b == 1:
            draw.rectangle([cur_x, by, cur_x + bw - 1, by + bh], fill=(10, 15, 25))
        cur_x += bw
    draw.text((190, by + bh + 6), "* KHA-PART-2026-8902 *", fill=(15, 23, 42), font=font_m)
    
    # 2D QR Code (Simulated realistic 25x25 matrix with 3 finder patterns)
    qrx, qry, qsize = 120, 310, 180
    cell = qsize // 25
    draw.rectangle([qrx - 10, qry - 10, qrx + qsize + 10, qry + qsize + 10], fill=(255, 255, 255), outline=(180, 190, 205), width=2)
    
    # Seeded pseudo-matrix
    rnd = random.Random(1337)
    for r in range(25):
        for c in range(25):
            # Check corners (Finder patterns: 7x7)
            is_finder = (r < 7 and c < 7) or (r < 7 and c >= 18) or (r >= 18 and c < 7)
            if is_finder:
                # 7x7 outer border, 3x3 inner square
                lr = r if r < 7 else (r - 18)
                lc = c if c < 7 else (c - 18 if c >= 18 else c)
                is_black = (lr == 0 or lr == 6 or lc == 0 or lc == 6) or (2 <= lr <= 4 and 2 <= lc <= 4)
                if is_black:
                    draw.rectangle([qrx + c * cell, qry + r * cell, qrx + (c+1)*cell - 1, qry + (r+1)*cell - 1], fill=(10, 15, 25))
            else:
                if rnd.random() > 0.45:
                    draw.rectangle([qrx + c * cell, qry + r * cell, qrx + (c+1)*cell - 1, qry + (r+1)*cell - 1], fill=(10, 15, 25))
                    
    draw.text((qrx, qry + qsize + 14), "QR Code (Model 2 ECC-M)", fill=(30, 41, 59), font=font_s)
    
    # 2D DataMatrix (Simulated 20x20 matrix with Solid L-finder)
    dmx, dmy, dmsize = 400, 310, 180
    dmcell = dmsize // 20
    draw.rectangle([dmx - 10, dmy - 10, dmx + dmsize + 10, dmy + dmsize + 10], fill=(255, 255, 255), outline=(180, 190, 205), width=2)
    
    for r in range(20):
        for c in range(20):
            # Solid L-border: Bottom (r=19) and Left (c=0)
            if r == 19 or c == 0:
                draw.rectangle([dmx + c * dmcell, dmy + r * dmcell, dmx + (c+1)*dmcell - 1, dmy + (r+1)*dmcell - 1], fill=(10, 15, 25))
            # Alternating timing border: Top (r=0) and Right (c=19)
            elif (r == 0 and c % 2 == 0) or (c == 19 and r % 2 == 1):
                draw.rectangle([dmx + c * dmcell, dmy + r * dmcell, dmx + (c+1)*dmcell - 1, dmy + (r+1)*dmcell - 1], fill=(10, 15, 25))
            elif rnd.random() > 0.48:
                draw.rectangle([dmx + c * dmcell, dmy + r * dmcell, dmx + (c+1)*dmcell - 1, dmy + (r+1)*dmcell - 1], fill=(10, 15, 25))
                
    draw.text((dmx, dmy + dmsize + 14), "DataMatrix (ECC 200)", fill=(30, 41, 59), font=font_s)

    im.save(os.path.join(OUTPUT_DIR, "02_industrial_codes_label.png"))
    print("Generated 02_industrial_codes_label.png")

# -------------------------------------------------------------
# 3. PCB Surface Defect Inspection (Scratch, Burr, Missing Hole)
# -------------------------------------------------------------
def generate_pcb_defect():
    im = Image.new("RGB", (WIDTH, HEIGHT), (12, 50, 32)) # Deep green solder mask
    draw = ImageDraw.Draw(im)
    
    # Ground copper plane grid pattern
    for x in range(0, WIDTH, 16):
        draw.line([(x, 0), (x, HEIGHT)], fill=(16, 62, 40), width=1)
        
    font_s = get_font(12)
    font_l = get_font(16)
    draw.text((20, 20), "SMT PCB CIRCUIT INSPECTION (DEFECT DETECTION)", fill=(245, 158, 11), font=font_l)
    draw.text((20, 44), "Defects: 1. Scratch Across Traces | 2. Solder Bridge Short | 3. Missing Via Hole", fill=(203, 213, 225), font=font_s)
    
    # Golden bus traces
    for y in [120, 150, 180, 210, 240, 270]:
        draw.line([(80, y), (700, y)], fill=(212, 175, 55), width=6)
        
    # IC Chip outline (QFP-32 footprint)
    ic_x1, ic_y1, ic_x2, ic_y2 = 300, 340, 500, 540
    draw.rectangle([ic_x1, ic_y1, ic_x2, ic_y2], fill=(24, 24, 27), outline=(63, 63, 70), width=2)
    draw.ellipse([ic_x1 + 15, ic_y1 + 15, ic_x1 + 30, ic_y1 + 30], fill=(113, 113, 122)) # Pin 1 index dot
    draw.text((ic_x1 + 45, ic_y1 + 80), "ARM CORTEX-M4\nKH-SMT-2026", fill=(161, 161, 170), font=font_s)
    
    # IC Pins (Left & Right)
    for p in range(8):
        py = ic_y1 + 25 + p * 22
        # Left pins
        draw.rectangle([ic_x1 - 35, py, ic_x1, py + 12], fill=(203, 213, 225), outline=(148, 163, 184))
        # Right pins
        draw.rectangle([ic_x2, py, ic_x2 + 35, py + 12], fill=(203, 213, 225), outline=(148, 163, 184))
        
    # Standard Via Holes with annular copper rings
    vias = [(120, 360), (160, 400), (200, 440), (240, 480), (620, 360), (660, 400), (700, 440)]
    for vx, vy in vias:
        draw.ellipse([vx - 14, vy - 14, vx + 14, vy + 14], fill=(212, 175, 55), outline=(245, 215, 110), width=2)
        draw.ellipse([vx - 6, vy - 6, vx + 6, vy + 6], fill=(5, 15, 10)) # drilled hole
        
    # DEFECT 1: Scratch across gold traces
    draw.line([(340, 110), (460, 280)], fill=(239, 68, 68), width=4) # Sharp red scratch trace
    draw.line([(342, 112), (462, 282)], fill=(20, 20, 20), width=2) # Dark groove
    draw.text((475, 180), "DEFECT 1: Scratch", fill=(239, 68, 68), font=font_s)
    
    # DEFECT 2: Solder Bridge / Burr shorting pins 3 & 4 on right side
    draw.rectangle([ic_x2 + 5, ic_y1 + 25 + 2 * 22, ic_x2 + 30, ic_y1 + 25 + 3 * 22 + 12], fill=(226, 232, 240))
    draw.text((ic_x2 + 45, ic_y1 + 25 + 2 * 22), "DEFECT 2: Solder Bridge", fill=(239, 68, 68), font=font_s)
    
    # DEFECT 3: Missing Via Hole (copper present, but un-drilled solid copper)
    mvx, mvy = 660, 480
    draw.ellipse([mvx - 14, mvy - 14, mvx + 14, mvy + 14], fill=(212, 175, 55), outline=(245, 215, 110), width=2)
    draw.text((mvx - 80, mvy + 20), "DEFECT 3: Missing Hole", fill=(239, 68, 68), font=font_s)

    im.save(os.path.join(OUTPUT_DIR, "03_pcb_circuit_defect.png"))
    print("Generated 03_pcb_circuit_defect.png")

# -------------------------------------------------------------
# 4. Geometric Matcher & Robot Pick (Rotated Metal Gear)
# -------------------------------------------------------------
def generate_gear_alignment():
    im = Image.new("RGB", (WIDTH, HEIGHT), (18, 22, 28))
    draw = ImageDraw.Draw(im)
    
    # Dark circular back-light ring
    draw.ellipse([80, 40, 720, 560], fill=(28, 34, 46), outline=(45, 55, 75), width=2)
    
    font_s = get_font(12)
    font_l = get_font(16)
    draw.text((20, 20), "GEOMETRIC PATTERN ALIGNMENT & ROBOT GUIDANCE", fill=(0, 210, 255), font=font_l)
    draw.text((20, 44), "Target: Industrial Spur Gear | Expected Pose: X=400, Y=300, Angle=25.0 deg", fill=(148, 163, 184), font=font_s)
    
    cx, cy = 400, 300
    angle_offset = math.radians(25.0)
    
    # Draw gear teeth (16 teeth)
    outer_r = 175
    inner_r = 140
    points = []
    num_teeth = 16
    step = 2 * math.pi / num_teeth
    
    for i in range(num_teeth):
        a1 = i * step + angle_offset
        a2 = a1 + step * 0.3
        a3 = a1 + step * 0.5
        a4 = a1 + step * 0.8
        
        points.append((cx + inner_r * math.cos(a1), cy + inner_r * math.sin(a1)))
        points.append((cx + outer_r * math.cos(a2), cy + outer_r * math.sin(a2)))
        points.append((cx + outer_r * math.cos(a3), cy + outer_r * math.sin(a3)))
        points.append((cx + inner_r * math.cos(a4), cy + inner_r * math.sin(a4)))
        
    draw.polygon(points, fill=(185, 195, 210), outline=(240, 245, 255))
    
    # Center mounting bore with keyway
    bore_r = 45
    draw.ellipse([cx - bore_r, cy - bore_r, cx + bore_r, cy + bore_r], fill=(18, 22, 28), outline=(130, 140, 155), width=2)
    # Keyway notch
    ka = angle_offset
    kx = cx + bore_r * math.cos(ka)
    ky = cy + bore_r * math.sin(ka)
    draw.rectangle([kx - 8, ky - 14, kx + 8, ky + 14], fill=(18, 22, 28))
    
    # 4 Weight-reduction circular lightening holes
    hole_r = 24
    dist = 90
    for h in range(4):
        ha = h * (math.pi / 2) + angle_offset + math.pi / 4
        hx = cx + dist * math.cos(ha)
        hy = cy + dist * math.sin(ha)
        draw.ellipse([hx - hole_r, hy - hole_r, hx + hole_r, hy + hole_r], fill=(18, 22, 28), outline=(140, 150, 165), width=2)
        
    # Coordinate fixture axes overlay
    draw.line([cx, cy, cx + 100 * math.cos(angle_offset), cy + 100 * math.sin(angle_offset)], fill=(239, 68, 68), width=3) # X-axis
    draw.line([cx, cy, cx - 100 * math.sin(angle_offset), cy + 100 * math.cos(angle_offset)], fill=(16, 185, 129), width=3) # Y-axis
    draw.text((cx + 105 * math.cos(angle_offset), cy + 105 * math.sin(angle_offset)), "X' (Fixture)", fill=(239, 68, 68), font=font_s)
    draw.text((cx - 105 * math.sin(angle_offset), cy + 105 * math.cos(angle_offset)), "Y' (Fixture)", fill=(16, 185, 129), font=font_s)

    im.save(os.path.join(OUTPUT_DIR, "04_gear_part_alignment.png"))
    print("Generated 04_gear_part_alignment.png")

# -------------------------------------------------------------
# 5. Pharmaceutical Tablet & Particle Counting (Blob Analysis)
# -------------------------------------------------------------
def generate_tablet_counting():
    im = Image.new("RGB", (WIDTH, HEIGHT), (20, 24, 38))
    draw = ImageDraw.Draw(im)
    
    font_s = get_font(12)
    font_l = get_font(16)
    draw.text((20, 20), "PHARMACEUTICAL BLISTER PACK (PARTICLE & BLOB COUNTING)", fill=(16, 185, 129), font=font_l)
    draw.text((20, 44), "Target: 12 Pockets (3x4 Grid) | Verify Presence, Centroid, Area & Missing Pill", fill=(148, 163, 184), font=font_s)
    
    # Blister pack tray
    bx1, by1, bx2, by2 = 100, 80, 700, 540
    draw.rounded_rectangle([bx1, by1, bx2, by2], radius=16, fill=(30, 38, 58), outline=(55, 68, 95), width=2)
    
    # 3 rows x 4 cols of pockets
    rows, cols = 3, 4
    pw, ph = 100, 95
    start_x, start_y = 150, 120
    gap_x, gap_y = 135, 130
    
    pill_count = 0
    for r in range(rows):
        for c in range(cols):
            x = start_x + c * gap_x
            y = start_y + r * gap_y
            
            # Pocket recess
            draw.ellipse([x - 45, y - 45, x + 45, y + 45], fill=(16, 20, 32), outline=(45, 55, 80), width=2)
            
            # Pocket (1, 2) is missing pill (DEFECT!)
            if r == 1 and c == 2:
                draw.text((x - 30, y - 8), "MISSING!", fill=(239, 68, 68), font=font_s)
                continue
                
            # Round white tablet
            draw.ellipse([x - 32, y - 32, x + 32, y + 32], fill=(245, 248, 255), outline=(210, 220, 235), width=2)
            # Imprint divider line
            draw.line([x - 22, y, x + 22, y], fill=(185, 195, 210), width=2)
            pill_count += 1
            draw.text((x - 8, y + 38), f"#{pill_count}", fill=(100, 116, 139), font=font_s)

    im.save(os.path.join(OUTPUT_DIR, "05_pharmaceutical_tablets.png"))
    print("Generated 05_pharmaceutical_tablets.png")

# -------------------------------------------------------------
# 6. Color Sorting & Bottle Cap Presence (Color & Presence)
# -------------------------------------------------------------
def generate_color_bottle_caps():
    im = Image.new("RGB", (WIDTH, HEIGHT), (26, 30, 40))
    draw = ImageDraw.Draw(im)
    
    font_s = get_font(12)
    font_l = get_font(16)
    draw.text((20, 20), "BOTTLING CONVEYOR LINE (COLOR CLASSIFICATION & PRESENCE)", fill=(244, 63, 94), font=font_l)
    draw.text((20, 44), "HSV Color Space Segmentation: Red, Green, Blue Caps | Detect Missing / Tilted Cap", fill=(148, 163, 184), font=font_s)
    
    # Stainless conveyor bed
    draw.rectangle([60, 80, 740, 540], fill=(40, 46, 60), outline=(65, 75, 95), width=2)
    for x in range(80, 740, 40):
        draw.line([(x, 80), (x, 540)], fill=(48, 55, 72), width=1)
        
    caps = [
        # (X, Y, ColorName, FillRGB, OutlineRGB, Status)
        (180, 200, "RED_CAP", (220, 38, 38), (252, 165, 165), "PASS (Red)"),
        (400, 200, "BLUE_CAP", (37, 99, 235), (147, 197, 253), "PASS (Blue)"),
        (620, 200, "GREEN_CAP", (16, 185, 129), (110, 231, 183), "PASS (Green)"),
        (180, 410, "BLUE_CAP", (37, 99, 235), (147, 197, 253), "PASS (Blue)"),
        (400, 410, "TILTED_CAP", (220, 38, 38), (252, 165, 165), "DEFECT (Tilted 15°)"),
        (620, 410, "MISSING", None, None, "DEFECT (Missing Cap)")
    ]
    
    r = 65
    for cx, cy, name, fill_color, out_color, status in caps:
        # Bottle mouth rim
        draw.ellipse([cx - r - 8, cy - r - 8, cx + r + 8, cy + r + 8], outline=(148, 163, 184), width=2)
        
        if fill_color:
            if "Tilted" in status:
                # Tilted ellipse
                draw.ellipse([cx - r + 15, cy - r, cx + r + 15, cy + r - 20], fill=fill_color, outline=out_color, width=3)
            else:
                # Normal circular cap with knurled grip ridges
                draw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=fill_color, outline=out_color, width=3)
                draw.ellipse([cx - r + 16, cy - r + 16, cx + r - 16, cy + r - 16], outline=out_color, width=2)
                
            color_text = (16, 185, 129) if "PASS" in status else (239, 68, 68)
            draw.text((cx - 45, cy + r + 12), status, fill=color_text, font=font_s)
        else:
            # Missing cap - hollow dark neck
            draw.ellipse([cx - r + 10, cy - r + 10, cx + r - 10, cy + r - 10], fill=(15, 23, 42), outline=(100, 116, 139), width=2)
            draw.text((cx - 55, cy + r + 12), status, fill=(239, 68, 68), font=font_s)

    im.save(os.path.join(OUTPUT_DIR, "06_colored_bottle_caps.png"))
    print("Generated 06_colored_bottle_caps.png")

# -------------------------------------------------------------
# 7. Circular Rubber O-Ring (Polar Unwrap & Radial Flaw)
# -------------------------------------------------------------
def generate_polar_seal_ring():
    im = Image.new("RGB", (WIDTH, HEIGHT), (240, 244, 250)) # Bright backlight table
    draw = ImageDraw.Draw(im)
    
    font_s = get_font(12)
    font_l = get_font(16)
    draw.text((20, 20), "CIRCULAR RUBBER SEAL O-RING (POLAR UNWRAP INSPECTION)", fill=(15, 23, 42), font=font_l)
    draw.text((20, 44), "Unwrap Polar (R, Theta) -> Linear Ribbon Strip | Check Flash Burr & Inner Notch", fill=(100, 116, 139), font=font_s)
    
    cx, cy = 400, 310
    outer_r = 190
    inner_r = 140
    
    # Outer black rubber circle
    draw.ellipse([cx - outer_r, cy - outer_r, cx + outer_r, cy + outer_r], fill=(24, 28, 36), outline=(10, 14, 20), width=2)
    
    # DEFECT 1: Flash / Burr on outer perimeter at 65 degrees
    burr_angle = math.radians(65)
    bx = cx + outer_r * math.cos(burr_angle)
    by = cy + outer_r * math.sin(burr_angle)
    draw.ellipse([bx - 12, by - 12, bx + 12, by + 12], fill=(24, 28, 36))
    draw.text((bx + 18, by - 10), "FLAW: Outer Burr", fill=(220, 38, 38), font=font_s)
    
    # Inner backlight hole
    draw.ellipse([cx - inner_r, cy - inner_r, cx + inner_r, cy + inner_r], fill=(240, 244, 250), outline=(10, 14, 20), width=2)
    
    # DEFECT 2: Inner Notch / Nick at 210 degrees
    notch_angle = math.radians(210)
    nx = cx + inner_r * math.cos(notch_angle)
    ny = cy + inner_r * math.sin(notch_angle)
    draw.ellipse([nx - 10, ny - 10, nx + 10, ny + 10], fill=(240, 244, 250))
    draw.text((nx - 120, ny + 10), "FLAW: Inner Notch", fill=(220, 38, 38), font=font_s)

    im.save(os.path.join(OUTPUT_DIR, "07_circular_seal_ring.png"))
    print("Generated 07_circular_seal_ring.png")

# -------------------------------------------------------------
# 8. Food Pack Expiry Date Code (OCR & Text Reading)
# -------------------------------------------------------------
def generate_expiry_date_ocr():
    im = Image.new("RGB", (WIDTH, HEIGHT), (250, 248, 240)) # Warm carton surface
    draw = ImageDraw.Draw(im)
    
    font_s = get_font(12)
    font_l = get_font(16)
    font_ocr = get_font(34)
    
    draw.text((20, 20), "PACKAGING INKJET EXPIRY DATE CODE (OPTICAL CHARACTER RECOGNITION)", fill=(30, 41, 59), font=font_l)
    draw.text((20, 44), "Simulated 7x5 Matrix Inkjet Print: Binarize -> OCR Bounding Boxes -> Text Validation", fill=(100, 116, 139), font=font_s)
    
    # Cardboard package window box
    draw.rounded_rectangle([100, 100, 700, 500], radius=12, fill=(255, 255, 255), outline=(210, 215, 225), width=2)
    
    # Product brand watermark
    draw.text((140, 130), "PREMIUM DAIRY MILK - 1000 mL", fill=(148, 163, 184), font=get_font(18))
    draw.line([(140, 165), (660, 165)], fill=(226, 232, 240), width=1)
    
    # Inkjet stamped date text (with simulated dot matrix / dark carbon black ink)
    lines = [
        ("EXP: 2026.12.31", 200, (15, 23, 42)),
        ("MFG: 2026.04.15", 260, (15, 23, 42)),
        ("LOT: KH-992-B20", 320, (15, 23, 42)),
        ("FACTORY CODE: VN-08", 380, (51, 65, 85))
    ]
    
    for text, y, col in lines:
        draw.text((160, y), text, fill=col, font=font_ocr)
        
    # OCR Bounding Box simulation overlays
    draw.rectangle([145, 190, 540, 255], outline=(16, 185, 129), width=2)
    draw.text((550, 210), "OCR ROI 1 [PASS]", fill=(16, 185, 129), font=font_s)
    
    draw.rectangle([145, 250, 540, 315], outline=(16, 185, 129), width=2)
    draw.text((550, 270), "OCR ROI 2 [PASS]", fill=(16, 185, 129), font=font_s)

    im.save(os.path.join(OUTPUT_DIR, "08_food_pack_expiry_date.png"))
    print("Generated 08_food_pack_expiry_date.png")

if __name__ == "__main__":
    generate_stepped_shaft()
    generate_industrial_codes()
    generate_pcb_defect()
    generate_gear_alignment()
    generate_tablet_counting()
    generate_color_bottle_caps()
    generate_polar_seal_ring()
    generate_expiry_date_ocr()
    print("ALL 8 SAMPLE TEST IMAGES GENERATED SUCCESSFULLY!")
