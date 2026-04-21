"""
Revert: convert AnyState transitions từ !u!1101 AnimatorStateTransition
trở về !u!1109 AnimatorTransition (format đúng của Unity cho AnyState).
"""
import re

F = r"w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller"
with open(F, "r", encoding="utf-8") as fp:
    raw = fp.read()
raw = raw.replace("\r\n", "\n")
lines = raw.split("\n")
print(f"Total lines: {len(lines)}")

# Tìm tất cả fileIDs trong m_AnyStateTransitions
any_state_fids = set()
for i, line in enumerate(lines):
    if re.match(r"\s+m_AnyStateTransitions:", line):
        for j in range(i + 1, len(lines)):
            if re.match(r"^--- !u!", lines[j]): break
            if re.match(r"\s+m_EntryTransitions:", lines[j]): break
            m = re.search(r"fileID: (\d+)", lines[j])
            if m: any_state_fids.add(m.group(1))

print(f"AnyState fids: {len(any_state_fids)}")

# Convert !u!1101 → !u!1109 for those fids (strip extra fields)
result = []
i = 0
converted = 0
while i < len(lines):
    m = re.match(r"^--- !u!1101 &(\d+)$", lines[i])
    if m and m.group(1) in any_state_fids:
        fid = m.group(1)
        # Parse conditions + dstState from the 1101 block
        conditions = []
        dst_state = "0"
        j = i + 1
        while j < len(lines):
            if j > i + 1 and re.match(r"^--- !u!", lines[j]): break
            t = lines[j].strip()
            if t == "m_Conditions:":
                pass
            elif t.startswith("- m_ConditionMode:"):
                conditions.append("  " + lines[j].strip())
            elif t.startswith("m_ConditionEvent:") or t.startswith("m_EventTreshold:"):
                conditions.append("    " + lines[j].strip())
            elif t.startswith("m_DstState:"):
                mm = re.search(r"fileID: (\d+)", t)
                if mm: dst_state = mm.group(1)
            j += 1

        # Emit correct !u!1109 AnimatorTransition
        result.append(f"--- !u!1109 &{fid}")
        result.append("AnimatorTransition:")
        result.append("  m_ObjectHideFlags: 1")
        result.append("  m_CorrespondingSourceObject: {fileID: 0}")
        result.append("  m_PrefabInstance: {fileID: 0}")
        result.append("  m_PrefabAsset: {fileID: 0}")
        result.append("  m_Name:")
        result.append("  m_Conditions:")
        result.extend(conditions)
        result.append("  m_DstStateMachine: {fileID: 0}")
        result.append(f"  m_DstState: {{fileID: {dst_state}}}")
        result.append("  m_Solo: 0")
        result.append("  m_Mute: 0")
        result.append("  m_IsExit: 0")
        result.append("  serializedVersion: 1")

        i = j
        converted += 1
        continue

    result.append(lines[i])
    i += 1

print(f"Reverted {converted} blocks from !u!1101 → !u!1109")

# Verify sample
for line in result:
    if "m_ConditionEvent: Shoot" in line:
        print(f"  Sample condition found: {line.strip()}")
        break

with open(F, "w", encoding="utf-8", newline="\n") as fp:
    fp.write("\n".join(result))
print(f"Done. Final lines: {len(result)}")
