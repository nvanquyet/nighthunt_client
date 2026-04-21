import re

F = r"w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller"
with open(F,"r",encoding="utf-8") as fp: raw=fp.read()
raw = raw.replace("\r\n","\n"); lines = raw.split("\n")

for fid in ["9200777","9200793","9200849"]:
    for i,line in enumerate(lines):
        m = re.match(r"^--- !u!(\d+) &" + fid, line)
        if m:
            print(f"fid {fid} -> !u!{m.group(1)} : {lines[i+1].strip()}")
            break
