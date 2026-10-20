"""Draws the icon of PasteFlow: a terracotta rounded square with a white sheet and a flowing stroke.

Run with Python 3 and Pillow: python tools/icon/make_icon.py src/PasteFlow.App/Assets/PasteFlow.ico
"""

import sys

from PIL import Image, ImageDraw

ACCENT = (201, 100, 66, 255)
ACCENT_LIGHT = (217, 119, 87, 255)
WHITE = (255, 255, 255, 255)
SIZE = 1024


def draw(size):
    scale = SIZE // 256
    big = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    pen = ImageDraw.Draw(big)

    # The tile, lighter at the top as a soft light
    top = Image.new("RGBA", (SIZE, SIZE), ACCENT_LIGHT)
    bottom = Image.new("RGBA", (SIZE, SIZE), ACCENT)
    fade = Image.linear_gradient("L").resize((SIZE, SIZE))
    tile = Image.composite(bottom, top, fade)
    mask = Image.new("L", (SIZE, SIZE), 0)
    ImageDraw.Draw(mask).rounded_rectangle((8 * scale, 8 * scale, 248 * scale, 248 * scale), radius=56 * scale, fill=255)
    big.paste(tile, (0, 0), mask)

    # A sheet of text, and a stroke flowing out of it to the right: a value going into a field
    thick = size < 24
    sheet = (50, 50, 150, 206) if not thick else (40, 44, 152, 212)
    pen.rounded_rectangle(tuple(v * scale for v in sheet), radius=(18 if not thick else 22) * scale, fill=WHITE)
    lines = ((86, 128), (112, 128), (138, 106)) if not thick else ((92, 122), (128, 104))
    for y, end in lines:
        half = 6 if not thick else 10
        pen.rounded_rectangle((72 * scale, (y - half) * scale, end * scale, (y + half) * scale), radius=half * scale, fill=ACCENT)

    arrow_y = 164 if not thick else 168
    width = 16 if not thick else 26
    start = 166 if not thick else 166
    pen.line([(start * scale, arrow_y * scale), (196 * scale, arrow_y * scale)], fill=WHITE, width=width * scale)
    pen.ellipse(((start - width // 2) * scale, (arrow_y - width // 2) * scale, (start + width // 2) * scale, (arrow_y + width // 2) * scale), fill=WHITE)
    head = 24 if not thick else 34
    pen.polygon([(190 * scale, (arrow_y - head) * scale), (220 * scale, arrow_y * scale), (190 * scale, (arrow_y + head) * scale)], fill=WHITE)

    return big.resize((size, size), Image.LANCZOS)


def main(path):
    sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    images = [draw(size) for size in sizes]
    images[-1].save(path, format="ICO", sizes=[(s, s) for s in sizes], append_images=images[:-1])
    preview = Image.new("RGBA", (sum(sizes) + 10 * len(sizes), 256), (250, 249, 245, 255))
    x = 0
    for image in images:
        preview.paste(image, (x, 0), image)
        x += image.width + 10
    preview.save(path.replace(".ico", "-apercu.png"))


if __name__ == "__main__":
    main(sys.argv[1])
