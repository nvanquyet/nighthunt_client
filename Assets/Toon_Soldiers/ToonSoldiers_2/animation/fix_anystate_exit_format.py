"""
fix_anystate_exit_format.py — Add proper serialized fields to AnyState Exit transitions
for Base Layer and UpperBody sub-machines (WeaponType!=N → IsExit blocks).

Adds: serializedVersion:3, TransitionDuration:0, TransitionOffset:0, ExitTime:0,
HasExitTime:0, HasFixedDuration:0, InterruptionSource:0, OrderedInterruption:1,
CanTransitionToSelf:0

These settings prevent:
  - Interrupting mid-transition (OrderedInterruption:1)
  - Re-firing on self (CanTransitionToSelf:0)
  - Instant snap (TransitionDuration:0 = still instant but explicit)
"""

import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F, 'r', encoding='utf-8') as f:
    lines = f.readlines()
print(f"Input: {len(lines)} lines")

# AnyState Exit FIDs: Base sub-machines + UB sub-machines + Unarmed machines
# Base exits: 9202133-9202138, Unarmed_Base exit: 9202203
# UB exits: 9202020, 9202043, 9202067, 9202090, 9202114, 9202132, Unarmed_UB: 9202213
# Death exits: no weapon exits needed in death sub-machines (Die trigger handles it)
ANYSTATE_EXIT_FIDS = {
    9202133, 9202134, 9202135, 9202136, 9202137, 9202138,  # Base layer exits
    9202203,                                                 # Unarmed_Base exit
    9202020, 9202043, 9202067, 9202090, 9202114, 9202132,  # UB exits
    9202213,                                                 # Unarmed_UB exit
    9202233,                                                 # Unarmed_Death prone exit (also anystate)
}

EXTRA_FIELDS = [
    '  serializedVersion: 3\n',
    '  m_TransitionDuration: 0\n',
    '  m_TransitionOffset: 0\n',
    '  m_ExitTime: 0\n',
    '  m_HasExitTime: 0\n',
    '  m_HasFixedDuration: 0\n',
    '  m_InterruptionSource: 0\n',
    '  m_OrderedInterruption: 1\n',
    '  m_CanTransitionToSelf: 0\n',
]

fixes = 0
i = 0
while i < len(lines):
    m = re.match(r'^--- !u!1101 &(\d+)', lines[i])
    if m and int(m.group(1)) in ANYSTATE_EXIT_FIDS:
        # Find m_IsExit: 1 line in this block
        for j in range(i, min(i + 20, len(lines))):
            if lines[j].strip() == 'm_IsExit: 1':
                # Check if already has serializedVersion
                if j + 1 >= len(lines) or 'serializedVersion' not in lines[j + 1]:
                    for k, fl in enumerate(EXTRA_FIELDS):
                        lines.insert(j + 1 + k, fl)
                    fixes += 1
                break
    i += 1

with open(F, 'w', encoding='utf-8') as f:
    f.writelines(lines)

print(f"Fixed {fixes} AnyState Exit transitions (added serialized fields)")
print(f"Output: {len(lines)} lines")
print()
print("Settings applied:")
print("  InterruptionSource: 0     = no interruption of current transition")
print("  OrderedInterruption: 1    = respect transition order")
print("  CanTransitionToSelf: 0    = won't re-fire to same state")
print("  HasExitTime: 0            = condition-based only, no time-based trigger")
