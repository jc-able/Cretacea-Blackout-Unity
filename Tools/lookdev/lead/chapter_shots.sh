#!/bin/bash
# The eight same-view chapter frames of the hand-over baseline (jump to a chapter, wait 5 s, Game view at 1920x1080),
# then a before/after sheet against ~/CretaceaBlackoutUnity/Screenshots/baseline.   chapter_shots.sh <out_dir_in_clone>
export UNITY_PROJECT_PATH=$HOME/CretaceaUnityClones/vfx
cd $UNITY_PROJECT_PATH && source Tools/lookdev/ucli.sh
O=${1:-Screenshots/after_final}; mkdir -p $O
unity command editor.playmode stop >/dev/null 2>&1; /bin/sleep 2
unity command editor.playmode start >/dev/null 2>&1; /bin/sleep 14
sandbox_saves >/dev/null
for CH in Intro Night Corridor ServicePaths Substation OldYard Escape DawnCoast; do
  unity command cretacea jump_chapter --chapter $CH >/dev/null 2>&1
  /bin/sleep 5
  gv "$PWD/$O/play_${CH}_5s.png" 1 1920 1080 >/dev/null
done
unity command editor.playmode stop >/dev/null 2>&1
/opt/homebrew/bin/python3 - "$O" <<'PYEOF'
import sys, os
from PIL import Image, ImageDraw, ImageFont
o=sys.argv[1]; base=os.path.expanduser("~/CretaceaBlackoutUnity/Screenshots/baseline")
ch=["Intro","Night","Corridor","ServicePaths","Substation","OldYard","Escape","DawnCoast"]
W,H=960,540; pad=6; head=46
try: font=ImageFont.truetype("/System/Library/Fonts/Helvetica.ttc",26); small=ImageFont.truetype("/System/Library/Fonts/Helvetica.ttc",20)
except Exception: font=small=ImageFont.load_default()
s=Image.new("RGB",(W*2+pad*3, head+(H+pad)*len(ch)+pad),(18,18,20)); d=ImageDraw.Draw(s)
d.text((pad+8,10),"BEFORE: the first Unity build, Nov 19",fill=(235,235,235),font=font)
d.text((W+pad*2+8,10),"AFTER: Nov 20, same jump, same five seconds",fill=(235,235,235),font=font)
for i,c in enumerate(ch):
    y=head+i*(H+pad)
    for j,f in enumerate((f"{base}/play_{c}_5s.png", f"{o}/play_{c}_5s.png")):
        x=pad+j*(W+pad)
        if os.path.exists(f): s.paste(Image.open(f).convert("RGB").resize((W,H),Image.LANCZOS),(x,y))
    d.rectangle((pad,y,pad+190,y+28),fill=(0,0,0)); d.text((pad+6,y+3),c,fill=(255,214,90),font=small)
s.save(f"{o}/before_after_chapters.jpg",quality=88); print("sheet", s.size, f"{o}/before_after_chapters.jpg")
PYEOF
