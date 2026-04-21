"""
Fix: convert tất cả AnyState transitions từ !u!1109 AnimatorTransition
thành !u!1101 AnimatorStateTransition (đúng format Unity yêu cầu).
"""
import re

F = r"w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller"
with open(F, "r", encoding="utf-8") as fp:
    raw = fp.read()
raw = raw.replace("\r\n", "\n")
lines = raw.split("\n")
print(f"Total lines: {len(lines)}")

# Tìm tất cả fileIDs được reference trong m_AnyStateTransitions
any_state_fids = set()
for i, line in enumerate(lines):
    if re.match(r"\s+m_AnyStateTransitions:", line):
        for j in range(i + 1, len(lines)):
            if re.match(r"^--- !u!", lines[j]):
                break
            if re.match(r"\s+m_EntryTransitions:", lines[j]):
                break
            m = re.search(r"fileID: (\d+)", lines[j])
            if m:
                any_state_fids.add(m.group(1))

print(f"AnyState fids found: {len(any_state_fids)}")

# Với mỗi !u!1109 block có fid trong any_state_fids → rebuild thành !u!1101
result = []
i = 0
converted = 0
while i < len(lines):
    m = re.match(r"^--- !u!1109 &(\d+)$", lines[i])
    if m and m.group(1) in any_state_fids:
        fid = m.group(1)
        # Parse the existing 1109 block to extract conditions + dstState
        conditions = []
        dst_state = "0"
        in_conds = False
        j = i + 1
        while j < len(lines):
            if j > i + 1 and re.match(r"^--- !u!", lines[j]):
                break
            line = lines[j]
            t = line.strip()
            if t == "m_Conditions:":
                in_conds = True
            elif in_conds and t.startswith("- m_ConditionMode:"):
                conditions.append(line)
            elif in_conds and (t.startswith("m_ConditionEvent:") or t.startswith("m_EventTreshold:")):
                conditions.append(line)
            elif t.startswith("m_DstState:"):
                mm = re.search(r"fileID: (\d+)", t)
                if mm:
                    dst_state = mm.group(1)
            elif t in ("m_Solo: 0", "m_Mute: 0", "m_IsExit: 0", "serializedVersion: 1",
                       "m_DstStateMachine: {fileID: 0}", "m_ObjectHideFlags: 1",
                       "m_CorrespondingSourceObject: {fileID: 0}",
                       "m_PrefabInstance: {fileID: 0}", "m_PrefabAsset: {fileID: 0}",
                       "m_Name: ", "m_Name:"):
                pass  # skip
            j += 1

        # Emit !u!1101 AnimatorStateTransition block
        result.append(f"--- !u!1101 &{fid}")
        result.append("AnimatorStateTransition:")
        result.append("  m_ObjectHideFlags: 1")
        result.append("  m_CorrespondingSourceObject: {fileID: 0}")
        result.append("  m_PrefabInstance: {fileID: 0}")
        result.append("  m_PrefabAsset: {fileID: 0}")
        result.append("  m_Name: ")
        result.append("  m_Conditions:")
        result.extend(conditions)
        result.append("  m_DstStateMachine: {fileID: 0}")
        result.append(f"  m_DstState: {{fileID: {dst_state}}}")
        result.append("  m_Solo: 0")
        result.append("  m_Mute: 0")
        result.append("  m_IsExit: 0")
        result.append("  serializedVersion: 3")
        result.append("  m_TransitionDuration: 0.05")
        result.append("  m_TransitionOffset: 0")
        result.append("  m_ExitTime: 0")
        result.append("  m_HasExitTime: 0")
        result.append("  m_HasFixedDuration: 0")
        result.append("  m_InterruptionSource: 0")
        result.append("  m_OrderedInterruption: 1")
        result.append("  m_CanTransitionToSelf: 0")

        i = j
        converted += 1
        continue

    result.append(lines[i])
    i += 1

print(f"Converted {converted} blocks from !u!1109 → !u!1101")

# Verify conditions were preserved
sample = [l for l in result if "m_ConditionEvent: Shoot" in l]
print(f"Shoot conditions found: {len(sample)}")

with open(F, "w", encoding="utf-8", newline="\n") as fp:
    fp.write("\n".join(result))
print(f"Done. Final line count: {len(result)}")
