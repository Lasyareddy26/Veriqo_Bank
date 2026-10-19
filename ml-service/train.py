"""Trains the TamperCNN on a real image-tampering dataset (CASIA 2.0 by default).

CASIA 2.0 layout after download: a folder of authentic images (…/Au/…) and a folder
of tampered images (…/Tp/…). We label by path, balance the classes, compute an ELA
image per sample, and train the CNN. The result is saved to model.pt and loaded by the
service at startup — training is a one-time step, never repeated per run.

Usage:
    python train.py --data dataset_casia --per-class 2500 --epochs 10
"""
import os
import glob
import argparse
import random
import numpy as np
import torch
import torch.nn as nn
from PIL import Image, ImageFile
from sklearn.model_selection import train_test_split

from forensics import ela_image
from model import TamperCNN

ImageFile.LOAD_TRUNCATED_IMAGES = True
ROOT = os.path.dirname(os.path.abspath(__file__))
DEVICE = torch.device("cuda" if torch.cuda.is_available() else "cpu")
EXTS = (".jpg", ".jpeg", ".png", ".tif", ".tiff", ".bmp")


def label_of(path: str):
    p = path.lower()
    if "groundtruth" in p or "_gt" in p or "mask" in p:
        return None  # ignore ground-truth tamper masks
    base = os.path.basename(p)
    if "/tp" in p or base.startswith("tp_") or "tamper" in p or "/forged" in p:
        return 1
    if "/au" in p or base.startswith("au_") or "authentic" in p or "/original" in p or "genuine" in p:
        return 0
    return None


def find_images(data_dirs):
    auth, tamp = [], []
    for data_dir in data_dirs:
        for path in glob.glob(os.path.join(data_dir, "**", "*"), recursive=True):
            if not path.lower().endswith(EXTS):
                continue
            lbl = label_of(path)
            if lbl == 0:
                auth.append(path)
            elif lbl == 1:
                tamp.append(path)
    return auth, tamp


def ela_tensor_fast(path, size=128, max_side=512):
    """Downscale big images before ELA (much faster), then ELA -> normalized CHW tensor."""
    img = Image.open(path).convert("RGB")
    if max(img.size) > max_side:
        img.thumbnail((max_side, max_side))
    ela = ela_image(img).resize((size, size))
    arr = np.asarray(ela, dtype=np.float32) / 255.0
    return np.transpose(arr, (2, 0, 1))


def collect_images(data_dir):
    """Every image under a dir, unlabeled (used for the document-authentic source)."""
    out = []
    for path in glob.glob(os.path.join(data_dir, "**", "*"), recursive=True):
        if path.lower().endswith(EXTS) and "groundtruth" not in path.lower():
            out.append(path)
    return out


def load(data_dirs, per_class):
    auth, tamp = find_images(data_dirs)
    random.shuffle(auth); random.shuffle(tamp)
    print(f"Pooled: {len(auth)} authentic, {len(tamp)} tampered from {data_dirs}")

    n = min(per_class, len(auth), len(tamp))
    chosen = [(p, 1) for p in tamp[:n]] + [(p, 0) for p in auth[:n]]
    random.shuffle(chosen)

    X, y = [], []
    for i, (path, lbl) in enumerate(chosen):
        try:
            X.append(ela_tensor_fast(path)); y.append(lbl)
        except Exception:
            continue
        if (i + 1) % 500 == 0:
            print(f"  processed {i + 1}/{len(chosen)} images…")
    return np.array(X, dtype=np.float32), np.array(y, dtype=np.float32)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--data", default="dataset_casia,dataset_doc",
                    help="comma-separated dataset folders (labelled by Au/Tp subpaths)")
    ap.add_argument("--per-class", type=int, default=3000)
    ap.add_argument("--epochs", type=int, default=12)
    args = ap.parse_args()

    random.seed(0); np.random.seed(0); torch.manual_seed(0)
    data_dirs = [os.path.join(ROOT, d.strip()) for d in args.data.split(",") if d.strip()]
    X, y = load(data_dirs, args.per_class)
    print(f"Loaded {len(X)} samples ({int(y.sum())} tampered)")

    Xtr, Xval, ytr, yval = train_test_split(X, y, test_size=0.2, stratify=y, random_state=0)
    Xtr, Xval = torch.tensor(Xtr), torch.tensor(Xval)
    ytr, yval = torch.tensor(ytr).unsqueeze(1), torch.tensor(yval).unsqueeze(1)

    model = TamperCNN().to(DEVICE)
    opt = torch.optim.Adam(model.parameters(), lr=1e-3, weight_decay=1e-4)
    loss_fn = nn.BCEWithLogitsLoss()

    n, bs = len(Xtr), 32
    best_score, best_state, best_stats = -1.0, None, None
    for epoch in range(args.epochs):
        model.train()
        perm = torch.randperm(n)
        total = 0.0
        for i in range(0, n, bs):
            idx = perm[i:i + bs]
            xb, yb = Xtr[idx].to(DEVICE), ytr[idx].to(DEVICE)
            opt.zero_grad()
            loss = loss_fn(model(xb), yb)
            loss.backward(); opt.step()
            total += loss.item() * len(idx)

        model.eval()
        with torch.no_grad():
            logits = model(Xval.to(DEVICE)).cpu()
            pred = (torch.sigmoid(logits) > 0.5).float()
            acc = (pred == yval).float().mean().item()
            tp = ((pred == 1) & (yval == 1)).sum().item()
            fp = ((pred == 1) & (yval == 0)).sum().item()
            fn = ((pred == 0) & (yval == 1)).sum().item()
            prec = tp / (tp + fp + 1e-9); rec = tp / (tp + fn + 1e-9)
            f1 = 2 * prec * rec / (prec + rec + 1e-9)
        print(f"epoch {epoch+1:2d}  loss={total/n:.4f}  val_acc={acc:.3f}  precision={prec:.3f}  recall={rec:.3f}  f1={f1:.3f}")
        # keep the best-balanced epoch (by F1), not the last — avoids a recall-heavy tail
        if f1 > best_score:
            best_score = f1
            best_state = {k: v.clone() for k, v in model.state_dict().items()}
            best_stats = (acc, prec, rec)

    torch.save(best_state, os.path.join(ROOT, "model.pt"))
    a, p, rc = best_stats
    print(f"Saved best model.pt  (val acc {a:.1%}, precision {p:.1%}, recall {rc:.1%}, f1 {best_score:.3f})")


if __name__ == "__main__":
    main()
