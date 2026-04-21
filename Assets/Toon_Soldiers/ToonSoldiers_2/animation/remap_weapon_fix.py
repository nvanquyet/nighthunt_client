"""
remap_weapon_fix.py — Remap WeaponType indices + fix defaults + smooth UB transitions

OLD mapping: 0=Handgun, 1=Infantry, 2=Heavy, 3=Knife, 4=Machinegun, 5=RL, 6=Unarmed
NEW mapping: 0=Unarmed(default), 1=Handgun, 2=Infantry, 3=Heavy, 4=Knife, 5=Machinegun, 6=RL

Changes:
  1. Remap all WeaponType condition thresholds in transitions (Entry + AnyState exits)
  2. Fix 3 fallback Entry transitions → Unarmed sub-machines (was Handgun)
  3. Add serializedVersion:3 + TransitionDuration:0.05 to UpperBody action AnyState transitions
"""

import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F, 'r', encoding='utf-8') as f:
    lines = f.readlines()
print(f"Input: {len(lines)} lines")

# ─────────────────────────────────────────────────────────────────────────────
# STEP 1: Remap WeaponType condition thresholds
# Old → New: 0→1, 1→2, 2→3, 3→4, 4→5, 5→6, 6→0
# Use temp placeholders to avoid collision (e.g. 0→1 then 1→2 would double-map)
# Placeholder mapping: add 100 first, then subtract 99
# ─────────────────────────────────────────────────────────────────────────────
REMAP = {0: 1, 1: 2, 2: 3, 3: 4, 4: 5, 5: 6, 6: 0}

wt_fixes = 0
for i in range(len(lines)):
    if 'm_ConditionEvent: WeaponType' in lines[i]:
        j = i + 1
        if j < len(lines) and 'm_EventTreshold:' in lines[j]:
            m = re.search(r'm_EventTreshold: (\d+)', lines[j])
            if m:
                old_val = int(m.group(1))
                new_val = REMAP.get(old_val, old_val)
                if new_val != old_val:
                    lines[j] = re.sub(r'm_EventTreshold: \d+', f'm_EventTreshold: {new_val}', lines[j])
                    wt_fixes += 1

print(f"[1] Remapped {wt_fixes} WeaponType condition thresholds")

# ─────────────────────────────────────────────────────────────────────────────
# STEP 2: Fix fallback Entry transitions to point to Unarmed sub-machines
# &9200781 (Base fallback) → was Handgun_Base (9200001), now Unarmed_Base (9202200)
# &9200819 (UB fallback)   → was HG_UB (9200048),       now Unarmed_UB (9202210)
# &9200857 (Death fallback)→ was HG_Death (9200111),     now Unarmed_Death (9202220)
# ─────────────────────────────────────────────────────────────────────────────
FALLBACK_MAP = {
    9200781: 9202200,  # Base  fallback → Unarmed_Base
    9200819: 9202210,  # UB    fallback → Unarmed_UpperBody
    9200857: 9202220,  # Death fallback → Unarmed_Death
}

fb_fixes = 0
i = 0
while i < len(lines):
    m = re.match(r'^--- !u!\d+ &(\d+)', lines[i])
    if m and int(m.group(1)) in FALLBACK_MAP:
        fid = int(m.group(1))
        new_dst = FALLBACK_MAP[fid]
        for j in range(i, min(i + 20, len(lines))):
            if '  m_DstStateMachine:' in lines[j]:
                old = lines[j]
                lines[j] = f'  m_DstStateMachine: {{fileID: {new_dst}}}\n'
                if old != lines[j]:
                    fb_fixes += 1
                    print(f"    &{fid}: DstStateMachine → {new_dst}")
                break
    i += 1

print(f"[2] Updated {fb_fixes} fallback Entry destinations to Unarmed")

# ─────────────────────────────────────────────────────────────────────────────
# STEP 3: Add TransitionDuration to UpperBody action AnyState transitions
# These are FIDs in range 9202000-9202132 EXCLUDING exit FIDs
# UB exit FIDs (WeaponType!=N exits): 9202020, 9202043, 9202067, 9202090, 9202114, 9202132
# These action transitions get: serializedVersion:3, TransitionDuration:0.05
# ─────────────────────────────────────────────────────────────────────────────
UB_ACTION_RANGE_START = 9202000
UB_ACTION_RANGE_END   = 9202132
UB_EXIT_FIDS = {9202020, 9202043, 9202067, 9202090, 9202114, 9202132}

DURATION_LINES = [
    '  serializedVersion: 3\n',
    '  m_TransitionDuration: 0.05\n',
    '  m_TransitionOffset: 0\n',
    '  m_ExitTime: 0\n',
    '  m_HasExitTime: 0\n',
    '  m_HasFixedDuration: 0\n',
    '  m_InterruptionSource: 0\n',
    '  m_OrderedInterruption: 1\n',
    '  m_CanTransitionToSelf: 0\n',
]

td_fixes = 0
i = 0
while i < len(lines):
    m = re.match(r'^--- !u!1101 &(\d+)', lines[i])
    if m:
        fid = int(m.group(1))
        if UB_ACTION_RANGE_START <= fid <= UB_ACTION_RANGE_END and fid not in UB_EXIT_FIDS:
            # Find m_IsExit: 0 in this block (action transitions, not exit)
            found_exit_zero = False
            for j in range(i, min(i + 25, len(lines))):
                if lines[j].strip() == 'm_IsExit: 0':
                    # Only add if serializedVersion not already present
                    if j + 1 >= len(lines) or 'serializedVersion' not in lines[j + 1]:
                        for k, dl in enumerate(DURATION_LINES):
                            lines.insert(j + 1 + k, dl)
                        td_fixes += 1
                    found_exit_zero = True
                    break
    i += 1

print(f"[3] Added TransitionDuration to {td_fixes} UB action AnyState transitions")

# ─────────────────────────────────────────────────────────────────────────────
# WRITE OUTPUT
# ─────────────────────────────────────────────────────────────────────────────
with open(F, 'w', encoding='utf-8') as f:
    f.writelines(lines)

print(f"\n=== DONE ===")
print(f"Output: {len(lines)} lines")
print(f"\nNew WeaponType mapping applied:")
print(f"  WT=0 → Unarmed (default, holster)")
print(f"  WT=1 → Handgun")
print(f"  WT=2 → Infantry")
print(f"  WT=3 → Heavy")
print(f"  WT=4 → Knife")
print(f"  WT=5 → Machinegun")
print(f"  WT=6 → RocketLauncher")
print(f"\nFallback Entry transitions now → Unarmed sub-machines (all 3 layers)")
