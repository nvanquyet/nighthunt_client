"""
fix_weapon_exit_trigger.py — Replace WeaponType!=N exit conditions with WeaponChanged trigger

Problem: WeaponType!=N is a continuous bool condition → spams exit every frame when WT changes
Solution: WeaponChanged trigger → fires ONCE, consumed immediately → clean single exit

Steps:
  1. Add WeaponChanged (Trigger, type=9) parameter after Roll
  2. On all AnyState exit blocks (Base + UB sub-machines): replace
       m_ConditionMode: 7 / WeaponType / N
     with
       m_ConditionMode: 1 / WeaponChanged / 0

Exit FIDs for Base layer:  9202133-9202138, 9202203
Exit FIDs for UB layer:    9202020, 9202043, 9202067, 9202090, 9202114, 9202132, 9202213
"""

import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F, 'r', encoding='utf-8') as f:
    lines = f.readlines()
print(f"Input: {len(lines)} lines")

# ─────────────────────────────────────────────────────────────────────────────
# STEP 1: Add WeaponChanged trigger parameter (after Roll)
# ─────────────────────────────────────────────────────────────────────────────
# Check if already exists
already = any('m_Name: WeaponChanged' in l for l in lines)
if already:
    print("[1] WeaponChanged already exists — skipped")
else:
    for i, l in enumerate(lines):
        if '  - m_Name: Roll' in l:
            # Find end of Roll block (next "  - m_Name:")
            j = i + 1
            while j < len(lines):
                if lines[j].strip().startswith('- m_Name:') or lines[j].startswith('  m_AnimatorLayers'):
                    break
                j += 1
            ins = [
                '  - m_Name: WeaponChanged\n',
                '    m_Type: 9\n',
                '    m_DefaultFloat: 0\n',
                '    m_DefaultInt: 0\n',
                '    m_DefaultBool: 0\n',
                '    m_Controller: {fileID: 9100000}\n',
            ]
            for k, line in enumerate(ins):
                lines.insert(j + k, line)
            print(f"[1] Added WeaponChanged trigger after Roll (at L{j+1})")
            break

# ─────────────────────────────────────────────────────────────────────────────
# STEP 2: Replace conditions on weapon exit AnyState transitions
# ─────────────────────────────────────────────────────────────────────────────
ANYSTATE_EXIT_FIDS = {
    # Base layer exits (WeaponType!=1..6 for each weapon, !=0 for Unarmed)
    9202133, 9202134, 9202135, 9202136, 9202137, 9202138,
    9202203,
    # UpperBody exits
    9202020, 9202043, 9202067, 9202090, 9202114, 9202132,
    9202213,
}

fixes = 0
i = 0
while i < len(lines):
    m = re.match(r'^--- !u!1101 &(\d+)', lines[i])
    if m and int(m.group(1)) in ANYSTATE_EXIT_FIDS:
        # Find and replace the condition block
        for j in range(i, min(i + 20, len(lines))):
            if lines[j].startswith('--- !u!') and j > i:
                break
            # Replace ConditionMode: 7 (NotEqual) with 1 (trigger If)
            if '  - m_ConditionMode: 7' in lines[j]:
                lines[j] = '  - m_ConditionMode: 1\n'
                fixes += 1
            # Replace WeaponType with WeaponChanged
            if '    m_ConditionEvent: WeaponType' in lines[j]:
                lines[j] = '    m_ConditionEvent: WeaponChanged\n'
                fixes += 1
            # Replace threshold (was the weapon index, now 0 for trigger)
            if '    m_EventTreshold:' in lines[j]:
                # Only replace if in the exit condition (preceded by WeaponChanged)
                # Check previous line
                prev = lines[j-1] if j > 0 else ''
                if 'WeaponChanged' in prev or 'WeaponType' in prev:
                    lines[j] = '    m_EventTreshold: 0\n'
    i += 1

print(f"[2] Replaced {fixes} condition fields on {len(ANYSTATE_EXIT_FIDS)} exit transitions")

# ─────────────────────────────────────────────────────────────────────────────
# WRITE OUTPUT
# ─────────────────────────────────────────────────────────────────────────────
with open(F, 'w', encoding='utf-8') as f:
    f.writelines(lines)

print(f"Output: {len(lines)} lines")

# Verify: check one exit block
with open(F, 'r', encoding='utf-8') as f:
    content = f.read()

block = re.search(r'--- !u!1101 &9202133\b(.*?)--- !u!', content, re.DOTALL)
if block:
    print('\nVerify &9202133 (Handgun_Base exit):')
    for line in block.group(1).splitlines():
        if line.strip():
            print(' ', line)

block2 = re.search(r'--- !u!1101 &9202020\b(.*?)--- !u!', content, re.DOTALL)
if block2:
    print('\nVerify &9202020 (Handgun_UB exit):')
    for line in block2.group(1).splitlines():
        if line.strip():
            print(' ', line)
