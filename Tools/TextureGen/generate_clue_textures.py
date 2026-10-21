"""
Erzeugt die Texturen für die Spuren-Modelle (Not My Blood).

Aufruf (aus dem Projektordner):
    python3 Tools/TextureGen/generate_clue_textures.py

Schreibt PNGs mit Transparenz nach Assets/08_Models/Textures:
    blood_pool.png        Blutlache mit Tropfen (Boden, Möbel)
    blood_spatter.png     Blutspritzer (Wand)
    shoe_kreise.png       zwei Schuhabdrücke, Profil Kreise
    shoe_zickzack.png     zwei Schuhabdrücke, Profil Zickzack
    shoe_glatt.png        zwei Schuhabdrücke, glattes Profil
    fabric_scrap.png      Stofffetzen mit Fäden (weiss, wird im Spiel eingefärbt)
    chalk_outline.png     Kreide-Umriss am Tatort
    warning_sign.png      Warnschild Hochspannung (Sicherungskasten)

Braucht Pillow und numpy (pip install pillow numpy).
Gleicher Seed = immer dieselben Bilder.
"""

import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "08_Models", "Textures"))

# Die Bilder werden mit doppelter Auflösung gezeichnet und dann verkleinert (weichere Kanten).
SS = 2


# ---------------------------------------------------------------- Helfer

def new_mask(w, h):
    return Image.new("L", (w * SS, h * SS), 0)


def finish(rgb, mask, w, h):
    """RGB-Array + Maske → RGBA-Bild in Zielgrösse. Alpha bleibt fast binär (für Alpha-Clipping)."""
    img = Image.fromarray(rgb.astype(np.uint8), "RGB").resize((w, h), Image.LANCZOS)
    a = mask.resize((w, h), Image.LANCZOS)
    out = img.convert("RGBA")
    out.putalpha(a)
    return out


def blob(cx, cy, radius, rng, lobes=6, roughness=0.18, points=120):
    """Unregelmässiger, runder Fleck als Polygon."""
    phases = [rng.uniform(0, math.tau) for _ in range(lobes)]
    amps = [rng.uniform(0.3, 1.0) * roughness / (1 + k * 0.45) for k in range(lobes)]
    pts = []
    for i in range(points):
        t = i / points * math.tau
        r = 1.0
        for k in range(lobes):
            r += amps[k] * math.sin((k + 2) * t + phases[k])
        pts.append((cx + math.cos(t) * radius * r, cy + math.sin(t) * radius * r))
    return pts


def ellipse_poly(cx, cy, rx, ry, angle, points=28):
    ca, sa = math.cos(angle), math.sin(angle)
    pts = []
    for i in range(points):
        t = i / points * math.tau
        x, y = math.cos(t) * rx, math.sin(t) * ry
        pts.append((cx + x * ca - y * sa, cy + x * sa + y * ca))
    return pts


def droplet(cx, cy, size, angle, stretch):
    """Tropfen mit Schweif, der vom Zentrum weg zeigt (angle = Flugrichtung)."""
    pts = []
    ca, sa = math.cos(angle), math.sin(angle)
    for i in range(32):
        t = i / 32 * math.tau
        x = math.cos(t)
        y = math.sin(t)
        if x > 0:   # vorne lang gezogen
            x *= stretch
            y *= max(0.25, 1.0 - 0.55 * (x / stretch))
        x *= size
        y *= size
        pts.append((cx + x * ca - y * sa, cy + x * sa + y * ca))
    return pts


def noise(h, w, rng, scale=8, amount=1.0):
    """Weiches Rauschen 0..1 (aus kleinem Zufallsbild hochskaliert)."""
    small = (np.asarray(rng.random((max(2, h // scale), max(2, w // scale)))) * 255).astype(np.uint8)
    img = Image.fromarray(small, "L").resize((w, h), Image.BICUBIC)
    return (np.asarray(img).astype(np.float32) / 255.0 - 0.5) * amount + 0.5


def mask_array(mask):
    return np.asarray(mask).astype(np.float32) / 255.0


def odd(n):
    """Filtergrössen von Pillow müssen ungerade sein."""
    n = int(n)
    return n if n % 2 else n + 1


# ---------------------------------------------------------------- Blut

def blood_pool(seed=11):
    rng = random.Random(seed)
    nrng = np.random.default_rng(seed)
    W = H = 512
    m = new_mask(W, H)
    d = ImageDraw.Draw(m)
    s = SS

    cx, cy, R = 250 * s, 262 * s, 128 * s
    d.polygon(blob(cx, cy, R, rng, lobes=7, roughness=0.22), fill=255)
    # zwei Nebenlachen, die in die Hauptlache laufen
    for ang, dist, rr in ((0.6, 0.95, 0.42), (3.6, 0.9, 0.35)):
        bx = cx + math.cos(ang) * R * dist
        by = cy + math.sin(ang) * R * dist
        d.polygon(blob(bx, by, R * rr, rng, lobes=5, roughness=0.2), fill=255)

    # Tropfen rundherum
    for _ in range(22):
        ang = rng.uniform(0, math.tau)
        dist = R * rng.uniform(1.25, 1.85)
        size = rng.uniform(3.5, 11) * s * (1.35 - (dist / R - 1.25))
        px, py = cx + math.cos(ang) * dist, cy + math.sin(ang) * dist
        if rng.random() < 0.55:
            d.polygon(droplet(px, py, size, ang, rng.uniform(1.6, 2.8)), fill=255)
        else:
            d.ellipse((px - size, py - size, px + size, py + size), fill=255)

    mask = m.filter(ImageFilter.GaussianBlur(1.2 * s)).point(lambda v: 255 if v > 120 else 0)
    ma = mask_array(mask)

    # Farbe: innen dunkel, Rand angetrocknet bräunlich, ein paar hellere Glanzstellen
    hh, ww = ma.shape
    inner = mask_array(mask.filter(ImageFilter.MinFilter(odd(17))))
    rim = np.clip(ma - inner, 0, 1)
    rim = np.asarray(Image.fromarray((rim * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(4 * s))) / 255.0
    yy, xx = np.mgrid[0:hh, 0:ww]
    dist = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / (R * 1.4)
    n = noise(hh, ww, nrng, scale=24 * s, amount=0.5)

    base = np.array([112, 12, 10], np.float32)
    dark = np.array([58, 4, 5], np.float32)
    dried = np.array([74, 22, 14], np.float32)
    t = np.clip(1.0 - dist, 0, 1)[..., None]
    rgb = base * (1 - t * 0.7) + dark * (t * 0.7)
    rgb = rgb * (0.85 + 0.3 * n[..., None])
    rgb = rgb * (1 - rim[..., None] * 0.8) + dried * (rim[..., None] * 0.8)
    return finish(np.clip(rgb, 0, 255), mask, W, H)


def blood_spatter(seed=23):
    rng = random.Random(seed)
    nrng = np.random.default_rng(seed)
    W = H = 512
    m = new_mask(W, H)
    d = ImageDraw.Draw(m)
    s = SS
    cx, cy = 256 * s, 256 * s

    d.polygon(blob(cx, cy, 46 * s, rng, lobes=6, roughness=0.28), fill=255)
    for _ in range(70):
        ang = rng.uniform(0, math.tau)
        dist = rng.uniform(55, 230) * s
        size = max(2.0, rng.uniform(4, 15) * (1.25 - dist / (260 * s))) * s
        px, py = cx + math.cos(ang) * dist, cy + math.sin(ang) * dist
        d.polygon(droplet(px, py, size, ang, rng.uniform(1.8, 3.6)), fill=255)
    for _ in range(90):   # feiner Nebel
        ang = rng.uniform(0, math.tau)
        dist = rng.uniform(40, 245) * s
        size = rng.uniform(1.5, 3.5) * s
        px, py = cx + math.cos(ang) * dist, cy + math.sin(ang) * dist
        d.ellipse((px - size, py - size, px + size, py + size), fill=255)

    mask = m.filter(ImageFilter.GaussianBlur(0.8 * s)).point(lambda v: 255 if v > 110 else 0)
    hh, ww = mask.size[1], mask.size[0]
    n = noise(hh, ww, nrng, scale=20 * s, amount=0.45)
    rgb = np.zeros((hh, ww, 3), np.float32) + np.array([96, 9, 8], np.float32)
    rgb = rgb * (0.8 + 0.35 * n[..., None])
    return finish(np.clip(rgb, 0, 255), mask, W, H)


# ---------------------------------------------------------------- Schuhabdrücke

def sole_polygon(cx, cy, length, width, angle, mirror):
    """Umriss einer Schuhsohle. t = 0 Ferse … 1 Spitze. mirror = rechter Schuh."""
    def half_width(t):
        heel = 0.72 * math.sqrt(max(0.0, min(1.0, t / 0.07)))
        if t < 0.3:
            w = heel
        elif t < 0.46:
            w = 0.72 - (t - 0.3) / 0.16 * 0.12          # Taille
        elif t < 0.72:
            w = 0.6 + (t - 0.46) / 0.26 * 0.4           # Ballen
        else:
            u = (t - 0.72) / 0.28                        # Spitze rund
            w = math.sqrt(max(0.0, 1 - u ** 2.4))
        return w * width * 0.5

    side_l, side_r = [], []
    steps = 80
    for i in range(steps + 1):
        t = i / steps
        hw = half_width(t)
        inner = hw * (0.8 if 0.3 < t < 0.6 else 1.0)   # Innenseite (Fussgewölbe) schmaler
        y = (t - 0.5) * length
        side_l.append((-inner, y))
        side_r.append((hw, y))
    pts = side_r + list(reversed(side_l))
    if mirror:
        pts = [(-x, y) for x, y in pts]
    ca, sa = math.cos(angle), math.sin(angle)
    return [(cx + x * ca - (-y) * sa, cy + x * sa + (-y) * ca) for x, y in pts]


def shoe_texture(profile, seed=5):
    rng = random.Random(seed)
    nrng = np.random.default_rng(seed)
    W, H = 512, 1024
    s = SS
    L, Wd = 400 * s, 150 * s

    prints = [  # (cx, cy, Winkel, rechts?)
        (165 * s, 690 * s, math.radians(-4), False),
        (350 * s, 330 * s, math.radians(5), True),
    ]

    sole = new_mask(W, H)
    ds = ImageDraw.Draw(sole)
    for cx, cy, ang, right in prints:
        ds.polygon(sole_polygon(cx, cy, L, Wd, ang, right), fill=255)

    # Lücke zwischen Absatz und Vorderfuss (bei allen Profilen)
    gap = new_mask(W, H)
    dg = ImageDraw.Draw(gap)
    for cx, cy, ang, right in prints:
        for t0 in (0.33,):
            y = (t0 - 0.5) * L
            ca, sa = math.cos(ang), math.sin(ang)
            p = []
            for (x, yy) in ((-Wd, y - 9 * s), (Wd, y - 9 * s), (Wd, y + 9 * s), (-Wd, y + 9 * s)):
                p.append((cx + x * ca - (-yy) * sa, cy + x * sa + (-yy) * ca))
            dg.polygon(p, fill=255)

    eroded = sole.filter(ImageFilter.MinFilter(odd(13 * s)))
    rim_arr = np.clip(mask_array(sole) - mask_array(eroded), 0, 1)
    rim = Image.fromarray((rim_arr * 255).astype(np.uint8), "L")

    pattern = new_mask(W, H)
    dp = ImageDraw.Draw(pattern)
    if profile == "kreise":
        step = 34 * s
        r_out, r_in = 12 * s, 6 * s
        for yy in range(0, H * s, step):
            off = (yy // step) % 2 * step // 2
            for xx in range(-step, W * s + step, step):
                x = xx + off
                dp.ellipse((x - r_out, yy - r_out, x + r_out, yy + r_out), fill=255)
                dp.ellipse((x - r_in, yy - r_in, x + r_in, yy + r_in), fill=0)
    elif profile == "zickzack":
        period, amp, gap_y, thick = 30 * s, 11 * s, 27 * s, 8 * s
        for y0 in range(-amp, H * s + amp, gap_y):
            pts = []
            for i, x in enumerate(range(-period, W * s + period, period // 2)):
                pts.append((x, y0 + (amp if i % 2 else -amp)))
            dp.line(pts, fill=255, width=thick, joint="curve")
    else:  # glatt: volle Fläche mit ein paar Fehlstellen (Dreck)
        dp.rectangle((0, 0, W * s, H * s), fill=255)
        for _ in range(120):
            x, y = rng.uniform(0, W * s), rng.uniform(0, H * s)
            r = rng.uniform(2, 6) * s
            dp.ellipse((x - r, y - r, x + r, y + r), fill=0)

    sa_ = mask_array(sole)
    combined = np.maximum(mask_array(rim), mask_array(pattern)) * sa_
    combined = combined * (1 - mask_array(gap))
    # abgenutzte Stellen: etwas Rauschen frisst kleine Löcher in den Abdruck
    wear = noise(H * s, W * s, nrng, scale=10 * s, amount=1.2)
    combined = combined * (wear > 0.1)
    mask = Image.fromarray((np.clip(combined, 0, 1) * 255).astype(np.uint8), "L")
    mask = mask.filter(ImageFilter.GaussianBlur(0.8 * s)).point(lambda v: 255 if v > 110 else 0)

    n = noise(H * s, W * s, nrng, scale=16 * s, amount=0.5)
    rgb = np.zeros((H * s, W * s, 3), np.float32) + np.array([58, 42, 30], np.float32)
    rgb = rgb * (0.8 + 0.4 * n[..., None])
    return finish(np.clip(rgb, 0, 255), mask, W, H)


# ---------------------------------------------------------------- Stofffetzen

def fabric_scrap(seed=7):
    rng = random.Random(seed)
    nrng = np.random.default_rng(seed)
    W = H = 256
    s = SS
    cx, cy = 128 * s, 132 * s

    m = new_mask(W, H)
    d = ImageDraw.Draw(m)
    # ausgefranster Umriss: grob rechteckig, Kanten gezackt
    pts = []
    corners = [(-62, -44), (58, -52), (66, 40), (-54, 50)]
    for i in range(4):
        x0, y0 = corners[i]
        x1, y1 = corners[(i + 1) % 4]
        for k in range(12):
            t = k / 12
            jx = rng.uniform(-7, 7) if k % 2 else rng.uniform(-2, 2)
            jy = rng.uniform(-7, 7) if k % 2 else rng.uniform(-2, 2)
            pts.append((cx + (x0 + (x1 - x0) * t + jx) * s, cy + (y0 + (y1 - y0) * t + jy) * s))
    d.polygon(pts, fill=255)

    # lose Fäden
    for _ in range(7):
        ang = rng.uniform(0, math.tau)
        x = cx + math.cos(ang) * 55 * s
        y = cy + math.sin(ang) * 45 * s
        line = [(x, y)]
        a = ang
        for _ in range(9):
            a += rng.uniform(-0.5, 0.5)
            x += math.cos(a) * 7 * s
            y += math.sin(a) * 7 * s
            line.append((x, y))
        d.line(line, fill=255, width=int(2.5 * s), joint="curve")

    mask = m.filter(ImageFilter.GaussianBlur(0.6 * s)).point(lambda v: 255 if v > 110 else 0)

    hh, ww = H * s, W * s
    yy, xx = np.mgrid[0:hh, 0:ww].astype(np.float32)
    # Webmuster: Kett- und Schussfäden
    weave_x = 0.5 + 0.5 * np.sin(xx / (2.2 * s))
    weave_y = 0.5 + 0.5 * np.sin(yy / (2.2 * s))
    checker = ((xx // (4.4 * s) + yy // (4.4 * s)) % 2)
    v = 0.8 + 0.1 * np.where(checker > 0, weave_x, weave_y) + 0.1 * noise(hh, ww, nrng, scale=12 * s, amount=0.6)
    rgb = np.stack([v, v, v], axis=-1) * 255
    return finish(np.clip(rgb, 0, 255), mask, W, H)


# ---------------------------------------------------------------- Kreide-Umriss

def chalk_outline(seed=3):
    rng = random.Random(seed)
    nrng = np.random.default_rng(seed)
    W, H = 512, 1024
    s = SS
    body = new_mask(W, H)
    d = ImageDraw.Draw(body)

    def capsule(p0, p1, r):
        d.line([(p0[0] * s, p0[1] * s), (p1[0] * s, p1[1] * s)], fill=255, width=int(2 * r * s))
        for p in (p0, p1):
            d.ellipse(((p[0] - r) * s, (p[1] - r) * s, (p[0] + r) * s, (p[1] + r) * s), fill=255)

    # Kopf oben, Körper liegt leicht verdreht
    d.ellipse((222 * s, 70 * s, 300 * s, 152 * s), fill=255)                 # Kopf
    capsule((262, 160), (256, 205), 22)                                        # Hals
    d.polygon([(p[0] * s, p[1] * s) for p in
               [(196, 200), (322, 206), (318, 330), (300, 470), (210, 468), (194, 330)]], fill=255)  # Rumpf
    capsule((205, 215), (120, 300), 26)                                        # linker Oberarm
    capsule((120, 300), (70, 410), 22)                                         # linker Unterarm
    d.ellipse((44 * s, 400 * s, 96 * s, 450 * s), fill=255)                    # Hand
    capsule((315, 220), (390, 170), 26)                                        # rechter Oberarm (nach oben)
    capsule((390, 170), (440, 90), 22)
    d.ellipse((418 * s, 52 * s, 470 * s, 102 * s), fill=255)
    capsule((230, 460), (190, 680), 34)                                        # linkes Bein
    capsule((190, 680), (170, 900), 28)
    d.polygon([(p[0] * s, p[1] * s) for p in [(140, 890), (205, 885), (215, 935), (130, 940)]], fill=255)
    capsule((285, 460), (340, 670), 34)                                        # rechtes Bein (angewinkelt)
    capsule((340, 670), (300, 880), 28)
    d.polygon([(p[0] * s, p[1] * s) for p in [(270, 872), (335, 875), (345, 925), (262, 922)]], fill=255)

    body = body.filter(ImageFilter.GaussianBlur(3 * s)).point(lambda v: 255 if v > 128 else 0)
    outer = body.filter(ImageFilter.MaxFilter(odd(11 * s)))
    line = np.clip(mask_array(outer) - mask_array(body), 0, 1)
    # Kreide ist rau: Löcher und unregelmässige Dicke
    grain = noise(H * s, W * s, nrng, scale=3 * s, amount=1.8)
    line = line * (grain > 0.18)
    mask = Image.fromarray((line * 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(0.7 * s)).point(lambda v: 255 if v > 100 else 0)
    rgb = np.zeros((H * s, W * s, 3), np.float32) + np.array([236, 234, 226], np.float32)
    return finish(rgb, mask, W, H)


# ---------------------------------------------------------------- Warnschild

def warning_sign():
    W = H = 256
    s = SS
    rgb_img = Image.new("RGB", (W * s, H * s), (0, 0, 0))
    m = new_mask(W, H)
    dm = ImageDraw.Draw(m)
    dr = ImageDraw.Draw(rgb_img)
    tri = [(128 * s, 18 * s), (240 * s, 224 * s), (16 * s, 224 * s)]
    dm.polygon(tri, fill=255)
    dr.polygon(tri, fill=(20, 20, 20))
    inner = [(128 * s, 50 * s), (214 * s, 208 * s), (42 * s, 208 * s)]
    dr.polygon(inner, fill=(250, 200, 20))
    bolt = [(140, 78), (100, 150), (128, 150), (112, 196), (160, 124), (132, 124), (150, 78)]
    dr.polygon([(x * s, y * s) for x, y in bolt], fill=(20, 20, 20))
    return finish(np.asarray(rgb_img).astype(np.float32), m, W, H)


# ---------------------------------------------------------------- Start

def main():
    os.makedirs(OUT, exist_ok=True)
    jobs = {
        "blood_pool.png": blood_pool,
        "blood_spatter.png": blood_spatter,
        "shoe_kreise.png": lambda: shoe_texture("kreise", seed=5),
        "shoe_zickzack.png": lambda: shoe_texture("zickzack", seed=6),
        "shoe_glatt.png": lambda: shoe_texture("glatt", seed=7),
        "fabric_scrap.png": fabric_scrap,
        "chalk_outline.png": chalk_outline,
        "warning_sign.png": warning_sign,
    }
    for name, fn in jobs.items():
        path = os.path.join(OUT, name)
        fn().save(path, optimize=True)
        print("geschrieben:", os.path.relpath(path, os.path.join(HERE, "..", "..")))


if __name__ == "__main__":
    main()
