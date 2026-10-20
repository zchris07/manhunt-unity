"""Draws the galaxy gas trap's icon (Assets/Vision/Resources/Icons/trap.png, 160x160) in the item icons' flat style: a
Galaxy Gas canister (a litre tank) standing upright: a white base ring, a tall label in watermelon pink over lemonade
yellow (pink drips running down into the yellow, a magenta band, the round logo, a slice of watermelon), a clear white
domed shoulder and a white spout tapering to its tip, all in a dark outline with a soft highlight down its left side.
Run: python tools/gas_icon.py"""
import os

from PIL import Image, ImageDraw

K = 4                 # drawn this many times larger, then smoothed down
N = 160 * K
OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'Vision', 'Resources', 'Icons', 'trap.png')

INK = (58, 52, 64, 255)
WHITE = (240, 240, 236, 255)
DOME = (226, 232, 236, 255)
YELLOW = (255, 208, 82, 255)
PINK = (255, 150, 172, 255)
MAGENTA = (200, 51, 122, 255)
RED = (232, 72, 92, 255)
GREEN = (88, 168, 76, 255)
SHINE = (255, 255, 255, 110)


def s(*v):
    """Design units (the 160 px canvas) to the drawing's."""
    return [x * K for x in v]


def main():
    img = Image.new('RGBA', (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    x0, x1 = 58, 102              # the body's sides
    top, bottom = 50, 150         # the label's top (the dome's foot) and the base's bottom
    ink = 3 * K

    # The spout and the dome, behind the body's top edge.
    d.polygon([tuple(s(76, 22)), tuple(s(84, 22)), tuple(s(81.5, 6)), tuple(s(78.5, 6))], fill=WHITE, outline=INK)
    d.line([tuple(s(76, 22)), tuple(s(78.5, 6)), tuple(s(81.5, 6)), tuple(s(84, 22))], fill=INK, width=ink, joint='curve')
    d.rounded_rectangle(s(74, 20, 86, 28), radius=2 * K, fill=WHITE, outline=INK, width=ink)
    d.ellipse(s(x0, 26, x1, 74), fill=DOME, outline=INK, width=ink)

    # The body: the label (pink over yellow), the base ring.
    d.rectangle(s(x0, top, x1, bottom - 12), fill=YELLOW)
    d.rectangle(s(x0, top, x1, 96), fill=PINK)
    # Pink drips running down into the yellow.
    for cx, length in ((64, 12), (73, 7), (83, 15), (94, 9)):
        d.rounded_rectangle(s(cx - 3, 92, cx + 3, 96 + length), radius=3 * K, fill=PINK)
        d.ellipse(s(cx - 3.6, 92 + length, cx + 3.6, 99 + length), fill=PINK)
    d.rectangle(s(x0, 56, x1, 61), fill=MAGENTA)
    # The round logo: a white disc, a magenta ring, a "G".
    d.ellipse(s(68, 66, 92, 90), fill=WHITE, outline=MAGENTA, width=2 * K)
    d.arc(s(73, 71, 87, 85), start=40, end=320, fill=MAGENTA, width=3 * K)
    d.line([tuple(s(80, 78)), tuple(s(86, 78))], fill=MAGENTA, width=3 * K)
    # A slice of watermelon on the yellow.
    d.pieslice(s(68, 108, 92, 132), start=0, end=180, fill=GREEN)
    d.pieslice(s(70, 108, 90, 128), start=0, end=180, fill=RED)
    for sx in (75, 80, 85):
        d.ellipse(s(sx - 1, 120, sx + 1, 123), fill=INK)
    # The base ring.
    d.rounded_rectangle(s(x0 - 1, bottom - 13, x1 + 1, bottom), radius=3 * K, fill=WHITE, outline=INK, width=ink)
    # The body's outline, and a highlight down its left side.
    d.line([tuple(s(x0, 50)), tuple(s(x0, bottom - 12))], fill=INK, width=ink)
    d.line([tuple(s(x1, 50)), tuple(s(x1, bottom - 12))], fill=INK, width=ink)
    d.rounded_rectangle(s(62, 32, 66, 132), radius=2 * K, fill=SHINE)

    img.resize((160, 160), Image.LANCZOS).save(OUT)
    print('wrote', os.path.normpath(OUT))


if __name__ == '__main__':
    main()
