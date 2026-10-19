"""Builds an in-domain document tamper set from REAL document scans (FINDIT).

Half the real documents are kept clean (authentic); the other half have real forgery
operations applied — copy-move (duplicate a block) and splice (overwrite a text region).
This is how document-tamper benchmarks (e.g. DocTamper) are constructed: real documents
with applied tampering. Output: dataset_doc/Au (clean) and dataset_doc/Tp (tampered).
"""
import os
import glob
import random
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(ROOT, "dataset_findit")
AU = os.path.join(ROOT, "dataset_doc", "Au")
TP = os.path.join(ROOT, "dataset_doc", "Tp")
FONT = "/System/Library/Fonts/Supplemental/Arial.ttf"


def font(sz):
    try:
        return ImageFont.truetype(FONT, sz)
    except Exception:
        return ImageFont.load_default()


def _splice(img, d, w, h):
    sw, sh = random.randint(w // 6, w // 3), random.randint(h // 30, h // 15)
    x, y = random.randint(0, max(w - sw, 1)), random.randint(0, max(h - sh, 1))
    bg = img.getpixel((min(x + 2, w - 1), min(y + 2, h - 1)))
    d.rectangle([x, y, x + sw, y + sh], fill=bg)
    d.text((x + 3, y + 1), f"{random.randint(100000, 9999999)}", fill=(18, 18, 18), font=font(max(sh - 3, 11)))


def _copymove(img, w, h):
    bw, bh = random.randint(w // 8, w // 4), random.randint(h // 16, h // 8)
    x, y = random.randint(0, max(w - bw, 1)), random.randint(0, max(h - bh, 1))
    patch = img.crop((x, y, x + bw, y + bh))
    nx = min(max(x + random.randint(bw // 2, bw), 0), w - bw)
    ny = min(max(y + random.randint(-bh, bh), 0), h - bh)
    img.paste(patch, (nx, ny))


def tamper(img):
    img = img.convert("RGB").copy()
    w, h = img.size
    d = ImageDraw.Draw(img)
    _splice(img, d, w, h)                 # always at least one clear splice
    if random.random() < 0.6:
        _splice(img, d, w, h)
    if random.random() < 0.5:
        _copymove(img, w, h)
    return img


VARIANTS = 3  # variants per source image, to grow the in-domain set


def build():
    os.makedirs(AU, exist_ok=True)
    os.makedirs(TP, exist_ok=True)
    imgs = [p for p in glob.glob(os.path.join(SRC, "**", "*"), recursive=True)
            if p.lower().endswith((".png", ".jpg", ".jpeg"))]
    random.shuffle(imgs)
    half = len(imgs) // 2
    na = nt = 0
    for i, p in enumerate(imgs):
        try:
            base = Image.open(p).convert("RGB")
            if max(base.size) > 1000:
                base.thumbnail((1000, 1000))
        except Exception:
            continue
        for v in range(VARIANTS):
            # Both classes are double-compressed at the SAME quality so Error Level Analysis
            # reflects the *edit*, not a compression difference. Authentic = saved→reopened→
            # resaved with no edit; tampered = same, but with a forgery applied before resave.
            if i < half:
                tmp = os.path.join(AU, f"doc_{i:04d}_{v}.jpg")
                base.save(tmp, "JPEG", quality=92)
                Image.open(tmp).save(tmp, "JPEG", quality=92)
                na += 1
            else:
                tmp = os.path.join(TP, f"doc_{i:04d}_{v}.jpg")
                base.save(tmp, "JPEG", quality=92)
                tamper(Image.open(tmp)).save(tmp, "JPEG", quality=92)
                nt += 1
    print(f"Built document set: {na} authentic, {nt} tampered")


if __name__ == "__main__":
    random.seed(1)
    build()
