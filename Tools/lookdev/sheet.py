"""Contact sheets: python3 sheet.py <out_prefix> <cols> <cell_w> file1 file2 ...  (labels = file names)"""
import sys, os
from PIL import Image, ImageDraw, ImageFont
out, cols, cw = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
files = sys.argv[4:]
ch = int(cw * 9 / 16)
per = cols * 4
try:
    font = ImageFont.truetype("/System/Library/Fonts/Helvetica.ttc", 15)
except Exception:
    font = ImageFont.load_default()
n = 0
for i in range(0, len(files), per):
    chunk = files[i:i + per]
    rows = (len(chunk) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * cw, rows * (ch + 22)), (20, 20, 20))
    d = ImageDraw.Draw(sheet)
    for j, f in enumerate(chunk):
        try:
            im = Image.open(f).convert("RGB")
        except Exception as e:
            continue
        im.thumbnail((cw, ch))
        x, y = (j % cols) * cw, (j // cols) * (ch + 22)
        sheet.paste(im, (x + (cw - im.width) // 2, y + 22 + (ch - im.height) // 2))
        d.text((x + 4, y + 3), os.path.basename(f)[:int(cw / 8.5)], fill=(255, 220, 120), font=font)
    n += 1
    p = f"{out}_{n:02d}.jpg"
    sheet.save(p, quality=82)
    print(p, sheet.size)
