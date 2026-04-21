"""
Strip extra fields from AnyState transition blocks.
Pre-existing working blocks end at m_IsExit: 0.
Our blocks have extra serializedVersion: 3, m_TransitionDuration etc. — strip them.
"""

import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'

with open(F, 'r', encoding='utf-8') as f:
    lines = f.readlines()

print(f"Total lines: {len(lines)}")

# Get all AnyState fids from all 6 weapon machines
MACHINE_FIDS = ['9200048', '9200173', '9200304', '9200438', '9200569', '9200703']
anystate_fids = set()

for mfid in MACHINE_FIDS:
    for i, l in enumerate(lines):
        if re.match(r'^--- !u!1107 &' + mfid + r'\b', l):
            j = i + 1
            in_any = False
            while j < len(lines) and not re.match(r'^--- !u!', lines[j]):
                if 'm_AnyStateTransitions:' in lines[j]:
                    in_any = True
                elif in_any and lines[j].strip().startswith('- {fileID:'):
                    m = re.search(r'fileID: (\d+)', lines[j])
                    if m:
                        anystate_fids.add(m.group(1))
                elif in_any and not lines[j].strip().startswith('- '):
                    in_any = False
                j += 1
            break

print(f"AnyState fids: {len(anystate_fids)}")

# Extra fields to strip (these appear after m_IsExit: 0 in our blocks)
EXTRA_FIELDS = {
    'serializedVersion:',
    'm_TransitionDuration:',
    'm_TransitionOffset:',
    'm_ExitTime:',
    'm_HasExitTime:',
    'm_HasFixedDuration:',
    'm_InterruptionSource:',
    'm_OrderedInterruption:',
    'm_CanTransitionToSelf:',
}

stripped = 0
lines_to_delete = set()

for fid in anystate_fids:
    for i, l in enumerate(lines):
        if re.match(r'^--- !u!1101 &' + fid + r'\b', l):
            # Find m_IsExit line in this block
            j = i + 1
            isExit_line = -1
            block_end = j
            while j < len(lines) and not re.match(r'^--- !u!', lines[j]):
                if 'm_IsExit:' in lines[j]:
                    isExit_line = j
                block_end = j
                j += 1
            
            if isExit_line < 0:
                break
            
            # Strip any extra fields after m_IsExit
            for k in range(isExit_line + 1, block_end + 1):
                stripped_line = lines[k].strip()
                for field in EXTRA_FIELDS:
                    if stripped_line.startswith(field):
                        lines_to_delete.add(k)
                        stripped += 1
                        break
            break

print(f"Lines to delete: {len(lines_to_delete)}")

# Rebuild lines without deleted ones
new_lines = [l for i, l in enumerate(lines) if i not in lines_to_delete]

print(f"Stripped {stripped} extra field lines from {len(anystate_fids)} AnyState blocks")

with open(F, 'w', encoding='utf-8') as f:
    f.writelines(new_lines)

print(f"Done. Final lines: {len(new_lines)}")
