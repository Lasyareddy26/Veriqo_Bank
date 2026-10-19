"""Generates sample KYC documents for demoing the OCR verification pipeline."""
from PIL import Image, ImageDraw, ImageFont
import os

OUT = os.path.dirname(os.path.abspath(__file__))

def font(size, bold=False):
    candidates = [
        "/System/Library/Fonts/Supplemental/Arial Bold.ttf" if bold else "/System/Library/Fonts/Supplemental/Arial.ttf",
        "/System/Library/Fonts/Helvetica.ttc",
    ]
    for c in candidates:
        if os.path.exists(c):
            try:
                return ImageFont.truetype(c, size)
            except Exception:
                pass
    return ImageFont.load_default()

def make(name, lines, w=1000, h=640, bg=(252, 250, 245)):
    img = Image.new("RGB", (w, h), bg)
    d = ImageDraw.Draw(img)
    d.rectangle([12, 12, w - 12, h - 12], outline=(180, 130, 100), width=4)
    y = 48
    for text, size, bold, color in lines:
        d.text((48, y), text, fill=color, font=font(size, bold))
        y += size + 22
    path = os.path.join(OUT, name)
    img.save(path, "PNG")
    print("wrote", path)

# 1. Government ID — matches profile (Meera Nair, 15/06/1995, P1234567)
make("government_id.png", [
    ("GOVERNMENT OF INDIA", 44, True, (40, 60, 120)),
    ("NATIONAL IDENTITY CARD", 30, False, (90, 90, 90)),
    ("", 10, False, (0, 0, 0)),
    ("Name:  MEERA NAIR", 40, True, (20, 20, 20)),
    ("Date of Birth:  15/06/1995", 36, False, (20, 20, 20)),
    ("ID No:  P1234567", 36, False, (20, 20, 20)),
    ("Valid Through:  20/12/2032", 32, False, (20, 20, 20)),
    ("Issued at:  MUMBAI, MAHARASHTRA", 28, False, (60, 60, 60)),
])

# 2. Address proof — recent electricity bill matching name + city
make("address_proof.png", [
    ("MUMBAI ELECTRIC SUPPLY CO.", 40, True, (30, 90, 70)),
    ("Electricity Bill / Proof of Address", 28, False, (90, 90, 90)),
    ("", 10, False, (0, 0, 0)),
    ("Customer:  MEERA NAIR", 36, True, (20, 20, 20)),
    ("Address:  12 MARINE DRIVE, MUMBAI 400020", 30, False, (20, 20, 20)),
    ("Bill Date:  05/10/2026", 32, False, (20, 20, 20)),
    ("Amount Due:  1,250.00", 30, False, (20, 20, 20)),
    ("Consumer No:  MH-88213490", 26, False, (60, 60, 60)),
])

# 3. Salary slip — income MISMATCH to show a risk flag.
#    Profile declares 900,000/yr. This slip shows 40,000/month => 480,000/yr (~47% lower).
make("salary_slip.png", [
    ("INFOSYS LTD", 42, True, (50, 50, 140)),
    ("Monthly Salary Slip - September 2026", 28, False, (90, 90, 90)),
    ("", 10, False, (0, 0, 0)),
    ("Employee:  MEERA NAIR", 36, True, (20, 20, 20)),
    ("Designation:  Senior Analyst", 28, False, (40, 40, 40)),
    ("Net Pay:  40,000.00", 40, True, (20, 20, 20)),
    ("Bank Credit:  40,000.00", 30, False, (20, 20, 20)),
    ("PAN:  ABCPN1234K", 26, False, (60, 60, 60)),
])
