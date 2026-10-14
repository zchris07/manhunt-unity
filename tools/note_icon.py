"""Draws the note's map icon (Assets/Vision/Resources/Icons/note.png, 160x160) in the item icons' flat style: a sheet of
old paper turned a little, its top corner folded over, a dark photo pinned to it with a red pin and two lines of ink
under it, a dark outline and a soft shadow so it reads on the painted map. Run: python tools/note_icon.py"""
import math
import os

from PIL import Image, ImageDraw, ImageFilter

K = 4                 # drawn this many times larger, then smoothed down
N = 160 * K
OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'Vision', 'Resources', 'Icons', 'note.png')

TURN = math.radians(-9)
CX, CY = N * 0.5, N * 0.53


def at(x, y):
    """A point of the sheet (centred units, about -1..1) on the canvas, turned."""
    s = N * 0.34
    x, y = x * s, y * s
    return (CX + x * math.cos(TURN) - y * math.sin(TURN), CY + x * math.sin(TURN) + y * math.cos(TURN))


def poly(d, pts, fill, outline=None, width=0):
    d.polygon([at(*p) for p in pts], fill=fill)
    if outline:
        q = [at(*p) for p in pts]
        d.line(q + [q[0]], fill=outline, width=width, joint='curve')


def main():
    w, h, fold = 0.78, 1.0, 0.3
    sheet = [(-w, -h), (w - fold, -h), (w, -h + fold), (w, h), (-w, h)]

    shadow = Image.new('RGBA', (N, N), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shadow)
    sd.polygon([(x + 5 * K, y + 7 * K) for x, y in (at(*p) for p in sheet)], fill=(0, 0, 0, 150))
    shadow = shadow.filter(ImageFilter.GaussianBlur(5 * K))

    img = Image.new('RGBA', (N, N), (0, 0, 0, 0))
    img.alpha_composite(shadow)
    d = ImageDraw.Draw(img)
    ink = (38, 30, 24, 255)
    # The sheet, aged paper.
    poly(d, sheet, (236, 226, 196, 255), ink, 5 * K)
    # A stain toward the bottom.
    d.ellipse([*at(-0.55, 0.45), *at(-0.1, 0.85)], fill=(222, 206, 168, 255))
    # The folded corner.
    poly(d, [(w - fold, -h), (w, -h + fold), (w - fold, -h + fold)], (200, 186, 150, 255), ink, 4 * K)
    # The photo: a dark, grainy square with a pale figure in it.
    photo = [(-0.52, -0.62), (0.4, -0.62), (0.4, 0.2), (-0.52, 0.2)]
    poly(d, photo, (58, 50, 44, 255), ink, 3 * K)
    poly(d, [(-0.42, -0.52), (0.3, -0.52), (0.3, 0.1), (-0.42, 0.1)], (92, 80, 68, 255))
    d.ellipse([*at(-0.13, -0.38), *at(0.03, -0.22)], fill=(214, 206, 188, 255))
    poly(d, [(-0.2, -0.2), (0.1, -0.2), (0.16, 0.1), (-0.26, 0.1)], (214, 206, 188, 255))
    # Two lines of ink.
    for y, x1 in ((0.42, 0.55), (0.62, 0.3)):
        d.line([at(-0.52, y), at(x1, y)], fill=(60, 48, 40, 255), width=5 * K)
    # The red pin holding the photo.
    px, py = at(-0.05, -0.7)
    r = 0.13 * N * 0.34
    d.ellipse([px - r, py - r, px + r, py + r], fill=(206, 44, 44, 255), outline=ink, width=3 * K)
    d.ellipse([px - r * 0.45 - r * 0.2, py - r * 0.45 - r * 0.25, px + r * 0.1, py - r * 0.05], fill=(255, 150, 140, 255))

    img = img.resize((160, 160), Image.LANCZOS)
    img.save(OUT)
    print(OUT)


if __name__ == '__main__':
    main()
