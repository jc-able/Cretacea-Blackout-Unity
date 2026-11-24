"""Fill playcap.cs for one species and run it in the (already playing) clone editor, then build contact sheets:
python3 playcap.py <Species> <x> <z> <yaw> <scenario,scenario,...>
Sheets: Screenshots/handin/play/sheet_<Species>_<scenario>.jpg (rows 1-2: side, rows 3-4: front/top; 8 frames each)."""
import os, sys, subprocess, glob
from PIL import Image
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '../../../../..'))
sp, x, z, yaw, scen = sys.argv[1:6]
src = open(os.path.join(HERE, 'playcap.cs')).read()
src = src.replace('@SP@', sp).replace('@X@', x).replace('@Z@', z).replace('@YAW@', yaw).replace('@SCEN@', scen)
tmp = os.path.join(ROOT, 'Screenshots/_work/playcap_run.cs'); os.makedirs(os.path.dirname(tmp), exist_ok=True)
open(tmp, 'w').write(src)
env = dict(os.environ, UNITY_PROJECT_PATH=ROOT, UNITY_NO_BANNER='1', PATH=os.path.expanduser('~/.unity/bin') + ':' + os.environ['PATH'])
r = subprocess.run(['unity', 'command', 'script', 'eval', '--file', tmp, '--timeout', '600'], capture_output=True, text=True, env=env)
print('\n'.join(l for l in (r.stdout + r.stderr).splitlines() if 'claude-code-hint' not in l)[-3000:])
out = os.path.join(ROOT, 'Screenshots/handin/play')
for sc in scen.split(','):
    views = ['side', 'top' if sc == 'turn' else 'front']
    sheet = Image.new('RGB', (1280, 720))
    for vi, view in enumerate(views):
        for k in range(8):
            f = os.path.join(out, f'{sp}_{sc}_{view}_{k:02d}.png')
            if not os.path.exists(f): continue
            im = Image.open(f).convert('RGB').resize((320, 180))
            sheet.paste(im, ((k % 4) * 320, (vi * 2 + k // 4) * 180))
    sheet.save(os.path.join(out, f'sheet_{sp}_{sc}.jpg'), quality=88)
print('sheets written')
