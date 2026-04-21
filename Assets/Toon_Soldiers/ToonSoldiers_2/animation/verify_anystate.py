import re

F = r"w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller"
with open(F,"r",encoding="utf-8") as fp: raw=fp.read()
raw = raw.replace("\r\n","\n"); lines = raw.split("\n")

# Find all AnyState fids
any_state_fids = set()
for i, line in enumerate(lines):
    if re.match(r"\s+m_AnyStateTransitions:", line):
        for j in range(i+1, len(lines)):
            if re.match(r"^--- !u!", lines[j]): break
            if re.match(r"\s+m_EntryTransitions:", lines[j]): break
            m = re.search(r"fileID: (\d+)", lines[j])
            if m: any_state_fids.add(m.group(1))

print(f"Total AnyState fids: {len(any_state_fids)}")

# Check each
not_found = []
found_1101 = []
found_1109 = []
for fid in sorted(any_state_fids, key=int):
    found = False
    for line in lines:
        m = re.match(r"^--- !u!(\d+) &" + fid, line)
        if m:
            t = m.group(1)
            if t == "1101": found_1101.append(fid)
            elif t == "1109": found_1109.append(fid)
            else: print(f"  UNEXPECTED type {t} for fid {fid}")
            found = True; break
    if not found:
        not_found.append(fid)

print(f"Correct !u!1101: {len(found_1101)}")
print(f"Still wrong !u!1109: {len(found_1109)} -> {found_1109[:5]}")
print(f"Missing (no block): {len(not_found)} -> {not_found[:10]}")
