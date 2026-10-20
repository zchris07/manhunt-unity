"""Draws the orbital strike's icon (Assets/Vision/Resources/Icons/strike.png, 160x160) in the item icons' flat style: a
rugged work tablet (the Modern Warfare killstreak tablet: a thick dark grey body, black rubber bumpers at its corners, a strap down its side) whose red screen
shows a map grid, a ring-and-crosshair reticle and a pink beam coming down on its middle.
Run: python tools/strike_icon.py"""
import os

from PIL import Image, ImageDraw

K = 4
N = 160 * K
OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'Vision', 'Resources', 'Icons', 'strike.png')

INK = (40, 38, 44, 255)
BODY = (52, 55, 60, 255)
EDGE = (78, 82, 88, 255)
BUMPER = (24, 25, 28, 255)
RIDGE = (70, 73, 78, 255)
SCREEN = (120, 22, 24, 255)
GRID = (170, 50, 50, 255)
RED = (255, 90, 80, 255)
PINK = (255, 105, 190, 255)
CORE = (255, 228, 245, 255)


def s(*v):
    return [x * K for x in v]


def main():
    img = Image.new('RGBA', (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    ink = 3 * K
    # The body, tipped a little, and its bevel.
    d.rounded_rectangle(s(18, 30, 142, 130), radius=12 * K, fill=BODY, outline=INK, width=ink)
    d.rounded_rectangle(s(23, 35, 137, 125), radius=9 * K, fill=EDGE)
    # The screen: a red map, a grid, the reticle, a beam on its middle.
    d.rectangle(s(31, 42, 129, 118), fill=SCREEN, outline=INK, width=2 * K)
    for x in range(41, 129, 12):
        d.line([tuple(s(x, 43)), tuple(s(x, 117))], fill=GRID, width=K)
    for y in range(52, 118, 12):
        d.line([tuple(s(32, y)), tuple(s(128, y))], fill=GRID, width=K)
    cx, cy = 80, 80
    d.polygon([tuple(s(cx - 6, 42)), tuple(s(cx + 6, 42)), tuple(s(cx + 9, cy)), tuple(s(cx - 9, cy))], fill=PINK)
    d.polygon([tuple(s(cx - 2, 42)), tuple(s(cx + 2, 42)), tuple(s(cx + 3, cy)), tuple(s(cx - 3, cy))], fill=CORE)
    d.ellipse(s(cx - 11, cy - 5, cx + 11, cy + 5), fill=PINK)
    d.ellipse(s(cx - 18, cy - 18, cx + 18, cy + 18), outline=RED, width=2 * K)
    for a, b in (((cx - 26, cy), (cx - 12, cy)), ((cx + 12, cy), (cx + 26, cy)), ((cx, cy - 26), (cx, cy - 12)), ((cx, cy + 12), (cx, cy + 26))):
        d.line([tuple(s(*a)), tuple(s(*b))], fill=RED, width=2 * K)
    # The bumpers.
    for x, y in ((14, 26), (126, 26), (14, 114), (126, 114)):
        d.rounded_rectangle(s(x, y, x + 20, y + 20), radius=5 * K, fill=BUMPER, outline=INK, width=ink)
        d.line([tuple(s(x + 6, y + 10)), tuple(s(x + 14, y + 10))], fill=RIDGE, width=K)
    # The strap's buttons on the side.
    d.rounded_rectangle(s(140, 62, 147, 98), radius=3 * K, fill=EDGE, outline=INK, width=2 * K)
    img.resize((160, 160), Image.LANCZOS).save(OUT)
    print('wrote', os.path.normpath(OUT))


if __name__ == '__main__':
    main()
