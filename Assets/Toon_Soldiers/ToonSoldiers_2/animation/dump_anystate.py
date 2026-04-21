import re

F = r"w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller"
with open(F,"r",encoding="utf-8") as fp: raw=fp.read()
raw=raw.replace("\r\n","\n"); lines=raw.split("\n")

# For each _UpperBody machine: show m_AnyStateTransitions content + resolve what each transition does
state_map = {}
for i,line in enumerate(lines):
    m = re.match(r"^--- !u!1102 &(\d+)", line)
    if m:
        for j in range(i+1, min(i+12,len(lines))):
            m2 = re.match(r"\s+m_Name:\s*(.*)$", lines[j])
            if m2: state_map[m.group(1)] = m2.group(1).strip(); break

# Build transition detail map: fid -> (conditions, dstState)
trans_map = {}
for i,line in enumerate(lines):
    m = re.match(r"^--- !u!1101 &(\d+)", line)
    if m:
        fid = m.group(1)
        conds = []
        dst = "0"
        for j in range(i+1, len(lines)):
            if j>i+1 and re.match(r"^--- !u!", lines[j]): break
            t = lines[j].strip()
            if t.startswith("m_ConditionEvent:"):
                conds.append(t.replace("m_ConditionEvent: ",""))
            if t.startswith("m_DstState:"):
                mm = re.search(r"fileID: (\d+)", t)
                if mm: dst = mm.group(1)
        dst_name = state_map.get(dst, f"fid:{dst}")
        trans_map[fid] = (conds, dst_name)

TARGET = ["Handgun_UpperBody","Infantry_UpperBody","Heavy_UpperBody",
          "Knife_UpperBody","Machinegun_UpperBody","RocketLauncher_UpperBody"]

for i,line in enumerate(lines):
    if re.match(r"^--- !u!1107 &", line):
        name = ""
        for j in range(i+1, min(i+14,len(lines))):
            m2 = re.match(r"\s+m_Name:\s*(.+)$", lines[j])
            if m2: name = m2.group(1).strip(); break
        if name not in TARGET: continue
        print(f"\n=== {name} ===")
        for j in range(i+1, len(lines)):
            if j>i+1 and re.match(r"^--- !u!", lines[j]): break
            if "m_AnyStateTransitions:" in lines[j]:
                print(f"  m_AnyStateTransitions:")
                for k in range(j+1, len(lines)):
                    if re.match(r"^--- !u!", lines[k]): break
                    if "m_EntryTransitions:" in lines[k]: break
                    if "m_AnyStateTransitions:" in lines[k]: break
                    mm = re.search(r"fileID: (\d+)", lines[k])
                    if mm:
                        ref = mm.group(1)
                        detail = trans_map.get(ref, ("???","???"))
                        print(f"    fid:{ref} -> {detail[0]} -> {detail[1]}")
