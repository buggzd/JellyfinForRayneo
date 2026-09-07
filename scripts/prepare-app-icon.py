"""Prepare Android launcher resources from the supplied glass-and-glasses artwork.

Run with Python 3 and Pillow. Crop/feather coordinates describe source.png, not
arbitrary replacement artwork; review the masks again when changing the source.
"""

from collections import deque
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
ARTWORK = ROOT / 'artwork/app-icon'
RESOURCES = ROOT / 'AndroidApp/app/src/main/res'
BACKGROUND = (2, 18, 29, 255)
CROP = (21, 16, 345, 340)
DENSITIES = {'mdpi': 1, 'hdpi': 1.5, 'xhdpi': 2, 'xxhdpi': 3, 'xxxhdpi': 4}
LANCZOS = Image.Resampling.LANCZOS


def glass_artwork(source):
    art = source.crop(CROP).convert('RGBA')
    # Fade only the outer dark tile into the native background. The luminous
    # glass panel stays intact; the screenshot's matte and baked corners vanish.
    mask = Image.new('L', art.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((10, 10, 313, 313), radius=74, fill=255)
    art.putalpha(mask.filter(ImageFilter.GaussianBlur(5)))
    return art


def monochrome_artwork(source):
    # Extract the connected sunglasses silhouette, retaining the play cutout.
    # Limit the region to the glasses so the surrounding dark tile is excluded.
    region = source.crop((62, 140, 306, 233)).convert('L')
    alpha = region.point(lambda value: max(0, min(255, (132 - value) * 255 // 32)))
    seed = (90, 30)
    visited = {seed}
    pending = deque([seed])
    while pending:
        x, y = pending.popleft()
        for xx, yy in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if (0 <= xx < alpha.width and 0 <= yy < alpha.height
                    and (xx, yy) not in visited and alpha.getpixel((xx, yy)) > 0):
                visited.add((xx, yy))
                pending.append((xx, yy))
    connected = Image.new('L', region.size, 0)
    for point in visited:
        connected.putpixel(point, alpha.getpixel(point))
    connected = connected.crop(connected.getbbox())
    centered = Image.new('L', (324, 324), 0)
    centered.paste(connected, ((324 - connected.width) // 2, (324 - connected.height) // 2))
    result = Image.new('RGBA', centered.size, 'white')
    result.putalpha(centered)
    return result


def adaptive_layer(art, size):
    # 108 dp layer, with the artwork in the central 72 dp viewport and 18 dp
    # bleed on every edge. The identifying glasses fit inside the 66 dp safe circle.
    content_size = round(size * 72 / 108)
    layer = Image.new('RGBA', (size, size))
    offset = (size - content_size) // 2
    layer.alpha_composite(art.resize((content_size, content_size), LANCZOS), (offset, offset))
    return layer


def shape_mask(size, circle=False):
    # Supersample masks for smooth edges even at mdpi.
    scale = 4
    mask = Image.new('L', (size * scale, size * scale), 0)
    draw = ImageDraw.Draw(mask)
    bounds = (0, 0, size * scale - 1, size * scale - 1)
    if circle:
        draw.ellipse(bounds, fill=255)
    else:
        draw.rounded_rectangle(bounds, radius=size * scale * .24, fill=255)
    return mask.resize((size, size), LANCZOS)


def legacy_icon(square, size, circle=False):
    # The 48 dp fallback includes a 2 dp optical inset for older icon consumers.
    content_size = round(size * 44 / 48)
    content = square.resize((content_size, content_size), LANCZOS)
    content.putalpha(shape_mask(content_size, circle))
    result = Image.new('RGBA', (size, size))
    offset = (size - content_size) // 2
    result.alpha_composite(content, (offset, offset))
    return result


def save_preview(square, monochrome):
    sheet = Image.new('RGB', (840, 330), '#e9eef4')
    draw = ImageDraw.Draw(sheet)
    draw.text((24, 18), 'Jellyfin for RayNeo | launcher icon', fill='#253c4b')
    for index, label in enumerate(('Rounded', 'Circle', 'Themed')):
        size = 192
        if label == 'Themed':
            tile = Image.new('RGBA', square.size, '#d8ece2')
            tile.paste('#234637', (0, 0, *tile.size), monochrome.getchannel('A'))
        else:
            tile = square
        tile = tile.resize((size, size), LANCZOS)
        tile.putalpha(shape_mask(size, circle=label == 'Circle'))
        x = 24 + index * 280
        sheet.paste(tile, (x, 55), tile)
        # Also show a realistic small launcher size to check the play symbol.
        small = tile.resize((48, 48), LANCZOS)
        sheet.paste(small, (x + 72, 263), small)
        draw.text((x, 317), label, fill='#253c4b')
    sheet.save(ARTWORK / 'preview.png', optimize=True)


def main():
    with Image.open(ARTWORK / 'source.png') as image:
        source = image.convert('RGB')
    if source.size != (370, 352):
        raise ValueError('Source dimensions changed; update the crop and silhouette coordinates.')
    art = glass_artwork(source)
    mono = monochrome_artwork(source)
    square = Image.new('RGBA', art.size, BACKGROUND)
    square.alpha_composite(art)
    for density, scale in DENSITIES.items():
        output = RESOURCES / f'mipmap-{density}'
        output.mkdir(parents=True, exist_ok=True)
        adaptive_layer(art, round(108 * scale)).save(output / 'ic_launcher_foreground.png', optimize=True)
        adaptive_layer(mono, round(108 * scale)).save(output / 'ic_launcher_monochrome.png', optimize=True)
        legacy_icon(square, round(48 * scale)).save(output / 'ic_launcher.png', optimize=True)
        legacy_icon(square, round(48 * scale), circle=True).save(output / 'ic_launcher_round.png', optimize=True)
    save_preview(square, mono)
    print('Prepared five launcher densities, adaptive layers, monochrome masks and preview.')


if __name__ == '__main__':
    main()
