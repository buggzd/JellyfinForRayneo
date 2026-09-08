"""Prepare Android launcher resources from the supplied tachi mech artwork.

Run with Python 3 and Pillow. Crop and monochrome contours describe source.png,
not arbitrary replacement artwork; review them again when changing the source.
"""

from math import hypot
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
ARTWORK = ROOT / 'artwork/app-icon'
RESOURCES = ROOT / 'AndroidApp/app/src/main/res'
CROP = (10, 14, 546, 550)
DENSITIES = {'mdpi': 1, 'hdpi': 1.5, 'xhdpi': 2, 'xxhdpi': 3, 'xxxhdpi': 4}
LANCZOS = Image.Resampling.LANCZOS


def mech_artwork(source):
    art = source.crop(CROP).convert('RGBA')
    # The source has baked rounded corners on a white presentation matte. Reflect
    # adjacent artwork into those corners so each launcher can apply its own mask.
    # Only the corner patches change; the lens, sensors and shell remain intact.
    radius = 80
    original = art.copy()
    for x in range(art.width):
        for y in range(art.height):
            cx = radius if x < radius else art.width - 1 - radius
            cy = radius if y < radius else art.height - 1 - radius
            if (radius <= x < art.width - radius
                    or radius <= y < art.height - radius):
                continue
            dx, dy = x - cx, y - cy
            distance = hypot(dx, dy)
            if distance > radius - 3:
                reflected = 2 * (radius - 3) - distance
                sample = (round(cx + dx * reflected / distance),
                          round(cy + dy * reflected / distance))
                art.putpixel((x, y), original.getpixel(sample))
    return art


def cubic_path(start, segments):
    points = [start]
    for control_one, control_two, end in segments:
        for step in range(1, 65):
            t = step / 64
            points.append(tuple(
                (1 - t) ** 3 * start[axis]
                + 3 * (1 - t) ** 2 * t * control_one[axis]
                + 3 * (1 - t) * t ** 2 * control_two[axis]
                + t ** 3 * end[axis] for axis in (0, 1)))
        start = end
    return points


def smooth_stroke(mask, points, width):
    # Rasterize above source resolution to avoid uneven stroke edges when the
    # sampled curve crosses pixel boundaries, then composite its smooth alpha.
    scale = 4
    stroke = Image.new('L', (mask.width * scale, mask.height * scale), 0)
    ImageDraw.Draw(stroke).line(
        [(x * scale, y * scale) for x, y in points],
        fill=255, width=width * scale, joint='curve')
    mask.paste(255, (0, 0), stroke.resize(mask.size, LANCZOS))


def monochrome_artwork():
    # Trace the supplied crop's shell contour, silver panel, sensors and lens.
    # A clean alpha silhouette survives launcher tinting better than photographic
    # luminance, which would merge the blue shell with the silver panel.
    alpha = Image.new('L', (536, 536), 0)
    draw = ImageDraw.Draw(alpha)
    panel = cubic_path((138, 536), [
        ((116, 450), (120, 344), (206, 258)),
        ((290, 174), (409, 161), (499, 207)),
        ((514, 214), (526, 222), (536, 230)),
    ])
    draw.polygon(panel + [(536, 536)], fill=255)
    for bounds in ((324, 265, 356, 294), (388, 269, 420, 299), (346, 310, 379, 341)):
        draw.ellipse(bounds, fill=0)
    # One continuous curve follows the shell from the lower left into the top
    # crop, passing the inner rim at (189, 151) without a segment-join kink.
    shell = cubic_path((-8, 452), [
        ((100, 204), (255, 51), (456, -8)),
    ])
    smooth_stroke(alpha, shell, width=11)
    rim = cubic_path((189, 151), [
        ((173, 190), (204, 214), (246, 192)),
        ((354, 146), (423, 124), (536, 180)),
    ])
    smooth_stroke(alpha, rim, width=11)
    lens = Image.new('L', (76, 130), 0)
    ImageDraw.Draw(lens).ellipse((6, 6, 69, 123), outline=255, width=10)
    lens = lens.rotate(-15, resample=Image.Resampling.BICUBIC, expand=True)
    alpha.paste(255, (101 - lens.width // 2, 355 - lens.height // 2), lens)
    result = Image.new('RGBA', alpha.size, 'white')
    result.putalpha(alpha)
    return result


def adaptive_layer(art, size):
    # Keep the close-up in the central 72 dp viewport of a 108 dp layer. Extend
    # edge pixels through the 18 dp bleed so launcher motion cannot reveal matte
    # or duplicate the lens. Color artwork is opaque, monochrome preserves alpha.
    edge = art.width
    padding = edge // 4
    layer = Image.new('RGBA', (edge + 2 * padding, edge + 2 * padding))
    source_spans = ((0, 1), (0, edge), (edge - 1, edge))
    target_spans = ((0, padding), (padding, padding + edge),
                    (padding + edge, layer.width))
    for row, (top, bottom) in enumerate(source_spans):
        for column, (left, right) in enumerate(source_spans):
            x0, x1 = target_spans[column]
            y0, y1 = target_spans[row]
            patch = art.crop((left, top, right, bottom))
            layer.paste(patch.resize((x1 - x0, y1 - y0)), (x0, y0))
    return layer.resize((size, size), LANCZOS)


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
    draw.text((24, 18), 'tachi | launcher icon', fill='#253c4b')
    for index, label in enumerate(('Rounded', 'Circle', 'Themed')):
        size = 192
        if label == 'Themed':
            tile = Image.new('RGBA', square.size, '#d8ece2')
            tile.paste('#234637', (0, 0, *tile.size), monochrome.getchannel('A'))
        else:
            tile = square.copy()
        tile = tile.resize((size, size), LANCZOS)
        tile.putalpha(shape_mask(size, circle=label == 'Circle'))
        x = 24 + index * 280
        sheet.paste(tile, (x, 55), tile)
        # Also show a realistic small launcher size to check the lens and sensors.
        small = tile.resize((48, 48), LANCZOS)
        sheet.paste(small, (x + 72, 263), small)
        draw.text((x, 317), label, fill='#253c4b')
    sheet.save(ARTWORK / 'preview.png', optimize=True)


def main():
    with Image.open(ARTWORK / 'source.png') as image:
        source = image.convert('RGB')
    if source.size != (571, 568):
        raise ValueError('Source dimensions changed; update the crop and silhouette coordinates.')
    art = mech_artwork(source)
    mono = monochrome_artwork()
    for density, scale in DENSITIES.items():
        output = RESOURCES / f'mipmap-{density}'
        output.mkdir(parents=True, exist_ok=True)
        adaptive_layer(art, round(108 * scale)).save(output / 'ic_launcher_foreground.png', optimize=True)
        adaptive_layer(mono, round(108 * scale)).save(output / 'ic_launcher_monochrome.png', optimize=True)
        legacy_icon(art, round(48 * scale)).save(output / 'ic_launcher.png', optimize=True)
        legacy_icon(art, round(48 * scale), circle=True).save(output / 'ic_launcher_round.png', optimize=True)
    save_preview(art, mono)
    print('Prepared five launcher densities, adaptive layers, monochrome masks and preview.')


if __name__ == '__main__':
    main()
