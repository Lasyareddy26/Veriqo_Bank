"""Classic image-forensics techniques used as tamper signals and CNN input."""
import io
import cv2
import numpy as np
from PIL import Image, ImageChops


def to_pil(data: bytes) -> Image.Image:
    return Image.open(io.BytesIO(data)).convert("RGB")


def ela_image(img: Image.Image, quality: int = 90) -> Image.Image:
    """Error Level Analysis: re-compress as JPEG and amplify the difference.
    Edited/spliced regions compress differently and light up brighter."""
    buf = io.BytesIO()
    img.save(buf, "JPEG", quality=quality)
    buf.seek(0)
    recompressed = Image.open(buf).convert("RGB")

    ela = ImageChops.difference(img, recompressed)
    extrema = ela.getextrema()
    max_diff = max(e[1] for e in extrema) or 1
    scale = 255.0 / max_diff
    return ela.point(lambda p: min(int(p * scale), 255))


def ela_tensor(img: Image.Image, size: int = 128) -> np.ndarray:
    """ELA image as a normalized (3, size, size) float array for the CNN."""
    ela = ela_image(img).resize((size, size))
    arr = np.asarray(ela, dtype=np.float32) / 255.0
    return np.transpose(arr, (2, 0, 1))  # HWC -> CHW


def ela_stats(img: Image.Image) -> dict:
    ela = np.asarray(ela_image(img), dtype=np.float32)
    gray = ela.mean(axis=2)
    return {
        "elaMean": round(float(gray.mean()), 3),
        "elaStd": round(float(gray.std()), 3),
        "elaMax": round(float(gray.max()), 3),
        "elaHotRatio": round(float((gray > 60).mean()), 4),  # fraction of "hot" pixels
    }


def copy_move_score(img: Image.Image) -> dict:
    """Detect duplicated regions (copy-move forgery) via ORB keypoint self-matching."""
    cv = cv2.cvtColor(np.array(img), cv2.COLOR_RGB2GRAY)
    orb = cv2.ORB_create(nfeatures=1500)
    kps, desc = orb.detectAndCompute(cv, None)
    if desc is None or len(kps) < 10:
        return {"copyMoveMatches": 0, "copyMoveRatio": 0.0}

    matcher = cv2.BFMatcher(cv2.NORM_HAMMING)
    knn = matcher.knnMatch(desc, desc, k=3)
    dup = 0
    for group in knn:
        for m in group:
            if m.queryIdx == m.trainIdx:
                continue
            p1, p2 = kps[m.queryIdx].pt, kps[m.trainIdx].pt
            dist = np.hypot(p1[0] - p2[0], p1[1] - p2[1])
            # similar descriptor + far apart in space => likely copied region
            if m.distance < 25 and dist > 40:
                dup += 1
            break
    return {"copyMoveMatches": int(dup), "copyMoveRatio": round(dup / max(len(kps), 1), 4)}


def noise_inconsistency(img: Image.Image) -> dict:
    """Spliced regions often carry different sensor noise. Measure block-wise
    high-frequency residual variance; high spread => inconsistent noise."""
    gray = cv2.cvtColor(np.array(img), cv2.COLOR_RGB2GRAY).astype(np.float32)
    residual = gray - cv2.GaussianBlur(gray, (3, 3), 0)
    h, w = residual.shape
    bh, bw = h // 8, w // 8
    if bh < 2 or bw < 2:
        return {"noiseInconsistency": 0.0}
    variances = [
        residual[y:y + bh, x:x + bw].var()
        for y in range(0, bh * 8, bh) for x in range(0, bw * 8, bw)
    ]
    variances = np.array(variances)
    spread = variances.std() / (variances.mean() + 1e-6)
    return {"noiseInconsistency": round(float(spread), 4)}


def all_signals(img: Image.Image) -> dict:
    s = {}
    s.update(ela_stats(img))
    s.update(copy_move_score(img))
    s.update(noise_inconsistency(img))
    return s
