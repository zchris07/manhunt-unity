"""Paints the orbital strike's tablet as held up to the face (Assets/Vision/Resources/Overlays/strike_tablet.png, 1600x1100,
the screen left clear for the game to draw its map into): the K.P.O.S., a rugged military tablet as the Modern Warfare
killstreak tablet looks, photographic rather than flat. A thick body of dark grey rubberised polymer with a fine stipple
and a soft sheen rolling over it, a raised bevel lit from above (bright along the top edge, falling into shadow at the
bottom), black moulded rubber impact bumpers wrapping its four corners with grip ridges and a faint sheen of their own, recessed hex screws, a row of hard keys down the right side (lit
function keys and a power key), a speaker grille and a camera lens along the top, a stencilled model number and a small
dark warning plate, scuffs and grime in the corners, the screen recessed behind a black glass border with a thin reflection across it.
Run: python tools/strike_tablet.py (numpy and Pillow)."""
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

W, H = 1600, 1100
SCREEN = (190, 150, 1410, 950)          # the clear screen window (left, top, right, bottom)
OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'Vision', 'Resources', 'Overlays', 'strike_tablet.png')
rng = np.random.default_rng(7)


def rounded_mask(box, radius, size=(W, H)):
    m = Image.new('L', size, 0)
    ImageDraw.Draw(m).rounded_rectangle(box, radius=radius, fill=255)
    return m


def noise(scale, octaves=3):
    """Smooth value noise over the canvas, 0-1."""
    out = np.zeros((H, W), np.float32)
    amp, total = 1.0, 0.0
    for o in range(octaves):
        s = max(2, int(scale / (2 ** o)))
        g = rng.random((H // s + 2, W // s + 2)).astype(np.float32)
        img = Image.fromarray((g * 255).astype(np.uint8)).resize((W + 2 * s, H + 2 * s), Image.BICUBIC)
        out += np.asarray(img, np.float32)[s:s + H, s:s + W] / 255.0 * amp
        total += amp
        amp *= 0.5
    return out / total


def shade(base, lit):
    """A colour (rgb) lit by a 0-2 factor per pixel: HxWx3."""
    return np.clip(np.asarray(base, np.float32)[None, None, :] * lit[..., None], 0, 255)


def main():
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    canvas = np.zeros((H, W, 4), np.float32)

    # The body: rubberised polymer, stippled, a soft sheen rolling diagonally over it, lit from above.
    body_mask = np.asarray(rounded_mask((20, 20, W - 20, H - 20), 110), np.float32) / 255.0
    stipple = rng.normal(0.0, 0.05, (H, W)).astype(np.float32)
    blotch = noise(220) - 0.5
    sheen = np.exp(-((xx * 0.55 + yy - 520) / 380.0) ** 2) * 0.22
    top_light = 1.12 - 0.32 * (yy / H)
    lit = top_light + sheen + stipple + blotch * 0.12
    body = shade((52, 54, 57), lit)
    canvas[..., :3] = body
    canvas[..., 3] = body_mask * 255

    # The raised bevel round the screen and round the edge: highlight above, shadow below (from a blurred edge map).
    def bevel(box, radius, width, strength):
        m = rounded_mask(box, radius)
        inner = rounded_mask((box[0] + width, box[1] + width, box[2] - width, box[3] - width), max(4, radius - width))
        ring = np.asarray(m, np.float32) - np.asarray(inner, np.float32)
        hm = np.asarray(m.filter(ImageFilter.GaussianBlur(width)), np.float32) / 255.0
        gy = np.gradient(hm, axis=0)
        gx = np.gradient(hm, axis=1)
        light = (gy * 1.0 + gx * 0.35) * strength
        return ring / 255.0, light
    ring, light = bevel((20, 20, W - 20, H - 20), 110, 26, 260.0)
    canvas[..., :3] = np.clip(canvas[..., :3] + (light[..., None] * 18.0) * ring[..., None] + light[..., None] * 6.0 * body_mask[..., None], 0, 255)

    # The screen's recess: a black glass border stepping down into it, lit along its lower lip.
    recess = (SCREEN[0] - 46, SCREEN[1] - 46, SCREEN[2] + 46, SCREEN[3] + 46)
    rm = np.asarray(rounded_mask(recess, 26), np.float32) / 255.0
    glass = shade((10, 11, 13), 1.0 + noise(90) * 0.25)
    canvas[..., :3] = canvas[..., :3] * (1 - rm[..., None]) + glass * rm[..., None]
    _, lip = bevel(recess, 26, 14, -220.0)
    canvas[..., :3] = np.clip(canvas[..., :3] + lip[..., None] * 7.0, 0, 255)

    img = Image.fromarray(canvas.astype(np.uint8), 'RGBA')
    d = ImageDraw.Draw(img)

    # Black rubber impact bumpers wrapping the corners, with grip ridges and a faint sheen.
    for cx, cy, fx, fy in ((20, 20, 1, 1), (W - 20, 20, -1, 1), (20, H - 20, 1, -1), (W - 20, H - 20, -1, -1)):
        bw, bh = 230, 210
        x0, x1 = sorted((cx, cx + fx * bw))
        y0, y1 = sorted((cy, cy + fy * bh))
        bump = Image.new('RGBA', (W, H), (0, 0, 0, 0))
        bd = ImageDraw.Draw(bump)
        bd.rounded_rectangle((x0, y0, x1, y1), radius=105, fill=(26, 27, 30, 255))
        # Cut back to an L round the corner.
        ix0, ix1 = sorted((cx + fx * 90, cx + fx * bw + fx * 10))
        iy0, iy1 = sorted((cy + fy * 90, cy + fy * bh + fy * 10))
        bd.rectangle((ix0, iy0, ix1, iy1), fill=(0, 0, 0, 0))
        arr = np.asarray(bump, np.float32)
        a = arr[..., 3:4] / 255.0
        litb = (1.15 - 0.4 * (yy / H) + noise(60, 2) * 0.12 - 0.06)[..., None]
        rubber = np.clip(np.array([27, 28, 31], np.float32) * litb, 0, 255)
        base = np.asarray(img, np.float32)
        base[..., :3] = base[..., :3] * (1 - a) + rubber * a
        base[..., 3:4] = np.maximum(base[..., 3:4], arr[..., 3:4])
        img = Image.fromarray(base.astype(np.uint8), 'RGBA')
        d = ImageDraw.Draw(img)
        # Grip ridges and the moulded highlight.
        for k in range(5):
            o = 30 + k * 26
            d.line([(cx + fx * o, cy + fy * 18), (cx + fx * 18, cy + fy * o)], fill=(12, 12, 14, 255), width=6)
            d.line([(cx + fx * (o + 4), cy + fy * 20), (cx + fx * 20, cy + fy * (o + 4))], fill=(78, 81, 86, 150), width=2)

    # Recessed hex screws.
    for sx, sy in ((150, 90), (W - 150, 90), (150, H - 90), (W - 150, H - 90), (W // 2 - 360, 70), (W // 2 + 360, 70)):
        d.ellipse((sx - 17, sy - 17, sx + 17, sy + 17), fill=(22, 23, 26, 255))
        d.ellipse((sx - 12, sy - 12, sx + 12, sy + 12), fill=(96, 99, 104, 255))
        d.ellipse((sx - 12, sy - 12, sx + 4, sy + 4), fill=(140, 143, 148, 255))
        hexagon = [(sx + 6 * np.cos(a), sy + 6 * np.sin(a)) for a in np.linspace(0, 2 * np.pi, 7)[:-1]]
        d.polygon(hexagon, fill=(30, 31, 34, 255))

    # Hard keys down the right side: lit function keys and a power key.
    for i in range(5):
        ky = 300 + i * 110
        d.rounded_rectangle((W - 120, ky, W - 64, ky + 78), radius=12, fill=(28, 29, 32, 255), outline=(80, 83, 88, 255), width=3)
        d.rounded_rectangle((W - 112, ky + 8, W - 72, ky + 34), radius=8, fill=(70, 72, 78, 255))
        lamp = (255, 70, 60, 255) if i == 0 else (90, 255, 140, 220) if i == 4 else (210, 200, 160, 200)
        d.ellipse((W - 99, ky + 48, W - 85, ky + 62), fill=lamp)
    # The speaker grille and the camera lens along the top.
    for i in range(18):
        gx = W // 2 - 170 + i * 20
        d.ellipse((gx - 4, 86, gx + 4, 94), fill=(14, 14, 16, 255))
    d.ellipse((W // 2 + 230, 72, W // 2 + 262, 104), fill=(10, 10, 12, 255), outline=(90, 93, 98, 255), width=4)
    d.ellipse((W // 2 + 238, 80, W // 2 + 248, 90), fill=(90, 130, 200, 255))
    # Stencilled model number and a caution label.
    try:
        font = ImageFont.truetype('arialbd.ttf', 30)
        small = ImageFont.truetype('arial.ttf', 20)
    except OSError:
        font = small = ImageFont.load_default()
    d.text((300, H - 112), 'K.P.O.S.  KP-7 ORBITAL UPLINK', font=font, fill=(150, 152, 156, 230))
    d.rectangle((W - 560, H - 118, W - 330, H - 78), fill=(30, 31, 34, 255), outline=(96, 99, 104, 255), width=2)
    d.text((W - 548, H - 112), '! HIGH ENERGY', font=small, fill=(196, 198, 200, 255))
    # Scuffs and grime gathered in the corners and along the bottom.
    grime = noise(140, 3)
    corner = np.maximum(np.exp(-(((xx - 0) ** 2 + (yy - H) ** 2) / 240000.0)), np.exp(-(((xx - W) ** 2 + (yy - H) ** 2) / 240000.0)))
    arr = np.asarray(img, np.float32)
    dirt = np.clip((grime - 0.45) * 2.2, 0, 1) * (0.25 + corner * 0.6)
    arr[..., :3] = arr[..., :3] * (1 - dirt[..., None] * 0.45)
    scratches = Image.new('L', (W, H), 0)
    sd = ImageDraw.Draw(scratches)
    for _ in range(70):
        x, y = rng.integers(40, W - 40), rng.integers(40, H - 40)
        if SCREEN[0] - 40 < x < SCREEN[2] + 40 and SCREEN[1] - 40 < y < SCREEN[3] + 40:
            continue
        length, ang = rng.integers(10, 60), rng.random() * np.pi
        sd.line([(x, y), (x + length * np.cos(ang), y + length * np.sin(ang))], fill=int(rng.integers(40, 110)), width=1)
    arr[..., :3] = np.clip(arr[..., :3] + np.asarray(scratches, np.float32)[..., None] * 0.5, 0, 255)

    # The screen left clear; a thin glass reflection across its border.
    arr[SCREEN[1]:SCREEN[3], SCREEN[0]:SCREEN[2], 3] = 0
    img = Image.fromarray(arr.astype(np.uint8), 'RGBA')
    refl = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(refl).polygon([(SCREEN[0] - 40, SCREEN[1] + 120), (SCREEN[0] + 160, SCREEN[1] - 40), (SCREEN[0] + 230, SCREEN[1] - 40), (SCREEN[0] - 40, SCREEN[1] + 190)],
                                  fill=(255, 255, 255, 26))
    img = Image.alpha_composite(img, refl)
    # A soft drop shadow under the whole tablet.
    shadow = rounded_mask((40, 50, W - 10, H - 4), 110).filter(ImageFilter.GaussianBlur(14))
    out = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    out.paste((0, 0, 0, 120), (0, 0), shadow)
    out = Image.alpha_composite(out, img)
    # (The screen window clear of the shadow too: the game draws its map there.)
    o = np.asarray(out).copy()
    o[SCREEN[1]:SCREEN[3], SCREEN[0]:SCREEN[2], 3] = 0
    out = Image.fromarray(o, 'RGBA')
    out.save(OUT)
    print('wrote', os.path.normpath(OUT), 'screen', SCREEN)


if __name__ == '__main__':
    main()
