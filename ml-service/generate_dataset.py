"""Builds a synthetic training set: genuine bank documents and tampered copies.

Genuine docs are saved as JPEG. Tampered copies take a genuine JPEG and edit a
field (text-splice) or duplicate a region (copy-move), then re-save — leaving the
double-compression / local-error traces that Error Level Analysis exposes.
"""
import os
import random
import io
from PIL import Image, ImageDraw, ImageFont

OUT = os.path.dirname(os.path.abspath(__file__))
GEN = os.path.join(OUT, "dataset", "genuine")
TAM = os.path.join(OUT, "dataset", "tampered")
FONTS = [
    "/System/Library/Fonts/Supplemental/Arial.ttf",
    "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
    "/System/Library/Fonts/Supplemental/Times New Roman.ttf",
    "/System/Library/Fonts/Supplemental/Courier New.ttf",
]
FIRST = ["Meera", "Asha", "Ravi", "Arjun", "Neha", "Sanjay", "Priya", "Karan", "Divya", "Rahul"]
LAST = ["Nair", "Rao", "Sharma", "Patel", "Iyer", "Gupta", "Reddy", "Khan", "Das", "Menon"]
BANKS = ["MUMBAI ELECTRIC SUPPLY", "STATE BANK", "INFOSYS LTD", "GOVERNMENT OF INDIA", "HDFC BANK"]


def font(size, path=None):
    path = path or random.choice(FONTS)
    try:
        return ImageFont.truetype(path, size)
    except Exception:
        return ImageFont.load_default()


def rand_amount():
    return f"{random.randint(1, 9)},{random.randint(0,9)}{random.randint(0,9)}{random.randint(0,9)}.00"


def render_doc():
    """Draw a random document; return (image, list of (bbox, text) value fields)."""
    w, h = 900, 600
    bg = random.choice([(252, 250, 245), (255, 255, 255), (250, 250, 252)])
    img = Image.new("RGB", (w, h), bg)
    d = ImageDraw.Draw(img)
    d.rectangle([10, 10, w - 10, h - 10], outline=(160, 120, 90), width=3)

    name = f"{random.choice(FIRST)} {random.choice(LAST)}"
    d.text((40, 36), random.choice(BANKS), fill=(40, 60, 120), font=font(38, FONTS[1]))
    d.text((40, 96), "Official Document", fill=(90, 90, 90), font=font(24))

    fields = []
    y = 170
    rows = [
        ("Name:", name),
        ("ID No:", f"P{random.randint(1000000, 9999999)}"),
        ("Date:", f"{random.randint(1,28):02d}/{random.randint(1,12):02d}/202{random.randint(3,6)}"),
        ("Amount:", rand_amount()),
        ("Account:", f"{random.randint(10000000, 99999999)}"),
    ]
    for label, val in rows:
        d.text((40, y), label, fill=(60, 60, 60), font=font(28))
        vx, vy = 320, y
        f = font(30, FONTS[random.randint(0, 1)])
        d.text((vx, vy), val, fill=(20, 20, 20), font=f)
        bbox = d.textbbox((vx, vy), val, font=f)
        fields.append((bbox, val))
        y += 70
    return img, fields


def save_jpeg(img, path, quality=92):
    img.save(path, "JPEG", quality=quality)


def tamper(img, fields):
    """Return a tampered copy using a random forgery technique."""
    img = img.copy()
    d = ImageDraw.Draw(img)
    method = random.choice(["splice", "splice", "copymove"])

    if method == "splice":
        # overwrite a value field with a different number (classic document edit)
        bbox, _ = random.choice(fields)
        x0, y0, x1, y1 = bbox
        # paint over with background sampled just left of the field
        bg = img.getpixel((max(x0 - 10, 2), (y0 + y1) // 2))
        d.rectangle([x0 - 2, y0 - 2, x1 + 2, y1 + 2], fill=bg)
        new_val = rand_amount() if "." in str(_) else str(random.randint(1000000, 9999999))
        d.text((x0, y0), new_val, fill=(15, 15, 15), font=font(30, FONTS[random.randint(0, 3)]))
    else:
        # copy-move: duplicate a block of the document elsewhere
        bx, by = random.randint(40, 400), random.randint(170, 400)
        patch = img.crop((bx, by, bx + 180, by + 50))
        img.paste(patch, (bx + random.randint(120, 300), by + random.randint(-30, 60)))

    return img


def build(n_genuine=260):
    os.makedirs(GEN, exist_ok=True)
    os.makedirs(TAM, exist_ok=True)
    for i in range(n_genuine):
        img, fields = render_doc()
        save_jpeg(img, os.path.join(GEN, f"doc_{i:04d}.jpg"))
        # reload the saved JPEG, then tamper + resave (double compression on the edit)
        reopened = Image.open(os.path.join(GEN, f"doc_{i:04d}.jpg")).convert("RGB")
        t = tamper(reopened, fields)
        save_jpeg(t, os.path.join(TAM, f"doc_{i:04d}.jpg"))
    print(f"Generated {n_genuine} genuine + {n_genuine} tampered documents")


if __name__ == "__main__":
    random.seed(42)
    build()
