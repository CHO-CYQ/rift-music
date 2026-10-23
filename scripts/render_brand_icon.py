"""Render the original polygon SVG to a consistent PNG and multi-size Windows ICO.

Requires Pillow. The SVG is the only geometry/color source; no external font,
AI bitmap, Riot logo, or network access is used.
"""
from pathlib import Path
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw

assets = Path(__file__).resolve().parents[1] / 'src/RiftMusic.App/Assets'
sizes = (16, 20, 24, 32, 40, 48, 64, 128, 256)
source = ET.parse(assets / 'RiftMusic.svg').getroot()
scale = 4
canvas = Image.new('RGBA', (256 * scale, 256 * scale))
draw = ImageDraw.Draw(canvas)
for polygon in source.findall('{http://www.w3.org/2000/svg}polygon'):
    points = [tuple(float(v) * scale for v in pair.split(','))
              for pair in polygon.attrib['points'].split()]
    draw.polygon(points, fill=polygon.attrib['fill'])
canvas = canvas.resize((256, 256), Image.Resampling.LANCZOS)
canvas.save(assets / 'RiftMusic.png')
frames = []
for size in sizes:
    frame = canvas.resize((size, size), Image.Resampling.LANCZOS)
    # Suppress near-zero alpha ringing outside the vector silhouette.
    frame.putalpha(frame.getchannel('A').point(lambda value: 0 if value < 16 else value))
    frames.append(frame)
frames[-1].save(assets / 'RiftMusic.ico', sizes=[(s, s) for s in sizes], append_images=frames[:-1])

with Image.open(assets / 'RiftMusic.ico') as icon:
    assert icon.ico.sizes() == {(s, s) for s in sizes}
    for size in icon.ico.sizes():
        frame = icon.ico.getimage(size).convert('RGBA')
        assert frame.getpixel((0, 0))[3] == 0, ('nontransparent corner', size)
        assert any(pixel[3] >= 250 and max(abs(a - b) for a, b in zip(pixel[:3], (50, 27, 221))) <= 3
                   for pixel in frame.get_flattened_data()), ('missing opaque blue', size)
print('Rendered and decoded', len(sizes), 'ICO sizes:', sizes)
