"""Icône Vectra, style télémétrie course : tuile carbone, bande rouge, « V » blanc incliné en vecteur, traits de vitesse."""
import math
import sys
from PIL import Image, ImageDraw

S = 1024
SS = 4  # suréchantillonnage pour des bords nets
W = S * SS
out_dir = sys.argv[1]

BG = (13, 13, 13, 255)
CARBON = (20, 20, 20, 255)
RED = (225, 6, 0, 255)
WHITE = (242, 242, 242, 255)

img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

def P(x, y):
    return (x * SS, y * SS)

# Tuile aux angles légèrement adoucis.
radius = int(S * 0.12) * SS
d.rounded_rectangle([P(16, 16), P(S - 16, S - 16)], radius=radius, fill=BG)

# Texture carbone discrète (diagonales).
tex = Image.new("RGBA", (W, W), (0, 0, 0, 0))
td = ImageDraw.Draw(tex)
for k in range(-S, 2 * S, 22):
    td.line([P(k, 0), P(k + S, S)], fill=CARBON, width=7 * SS)
mask = Image.new("L", (W, W), 0)
ImageDraw.Draw(mask).rounded_rectangle([P(16, 16), P(S - 16, S - 16)], radius=radius, fill=255)
img.paste(tex, (0, 0), Image.composite(tex.split()[3], Image.new("L", (W, W), 0), mask))

# Bande rouge en haut (sous le bord arrondi).
band = Image.new("L", (W, W), 0)
ImageDraw.Draw(band).rectangle([P(0, 16), P(S, 92)], fill=255)
img.paste(Image.new("RGBA", (W, W), RED), (0, 0), Image.composite(band, Image.new("L", (W, W), 0), mask))

# « V » incliné (italique) : deux branches épaisses, la droite finit en pointe de vecteur.
skew = math.tan(math.radians(14))

def sk(x, y, base_y=760):
    return P(x + (base_y - y) * skew, y)

thick = 118
bottom = (452, 790)
left_top = (250, 250)
right_tip = (745, 215)

# Branche gauche (parallélogramme).
d.polygon([sk(*left_top), sk(left_top[0] + thick, left_top[1]), sk(bottom[0] + thick * 0.55, bottom[1]), sk(bottom[0] - thick * 0.55, bottom[1])], fill=WHITE)
# Branche droite jusqu'à la base de la pointe.
ang = math.atan2(right_tip[1] - bottom[1], right_tip[0] - bottom[0])
shaft_end = (right_tip[0] - 150 * math.cos(ang), right_tip[1] - 150 * math.sin(ang))
nx, ny = -math.sin(ang), math.cos(ang)
h = thick * 0.5
d.polygon([
    sk(bottom[0] - thick * 0.55, bottom[1]), sk(bottom[0] + thick * 0.55, bottom[1]),
    sk(shaft_end[0] + nx * h, shaft_end[1] + ny * h), sk(shaft_end[0] - nx * h, shaft_end[1] - ny * h),
], fill=WHITE)
# Pointe de flèche.
hw = 125
d.polygon([
    sk(*right_tip),
    sk(shaft_end[0] + nx * hw, shaft_end[1] + ny * hw),
    sk(shaft_end[0] - nx * hw, shaft_end[1] - ny * hw),
], fill=WHITE)

# Traits de vitesse rouges à gauche.
for y, x0, x1 in [(470, 70, 215), (560, 110, 245), (650, 150, 290)]:
    d.polygon([sk(x0, y - 16), sk(x1, y - 16), sk(x1, y + 16), sk(x0, y + 16)], fill=RED)

icon = img.resize((S, S), Image.LANCZOS)
icon.save(f"{out_dir}/vectra-1024.png")
icon.resize((256, 256), Image.LANCZOS).save(f"{out_dir}/vectra-256.png")
icon.save(f"{out_dir}/vectra.ico", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])

sheet = Image.new("RGBA", (1100, 330), (240, 240, 240, 255))
sheet.paste(Image.new("RGBA", (1100, 165), (25, 25, 30, 255)), (0, 165))
x = 10
for s in [150, 128, 64, 48, 32, 24, 16]:
    im = icon.resize((s, s), Image.LANCZOS)
    sheet.alpha_composite(im, (x, (165 - s) // 2))
    sheet.alpha_composite(im, (x, 165 + (165 - s) // 2))
    x += s + 30
sheet.save(f"{out_dir}/vectra-sheet.png")
print("ok")
