"""
fix_anystate_exit_settings.py — Fix InterruptionSource and CanTransitionToSelf
for all WeaponType exit AnyState transitions.

InterruptionSource: 1 (CurrentState) = allows re-interrupting → LOOP RISK
CanTransitionToSelf: 1 = allows re-transitioning to same state → LOOP RISK

Fix: InterruptionSource: 0 + CanTransitionToSelf: 0
Also: TransitionDuration: 0 (instant weapon switch) + ExitTime: 0
"""

import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F, 'r', encoding='utf-8') as f:
    lines = f.readlines()
print(f"Input: {len(lines)} lines")

ANYSTATE_EXIT_FIDS = {
    9202133, 9202134, 9202135, 9202136, 9202137, 9202138,
    9202203,
    9202020, 9202043, 9202067, 9202090, 9202114, 9202132,
    9202213,
}

fixes = 0
i = 0
while i < len(lines):
    m = re.match(r'^--- !u!1101 &(\d+)', lines[i])
    if m and int(m.group(1)) in ANYSTATE_EXIT_FIDS:
        fid = int(m.group(1))
        for j in range(i, min(i + 30, len(lines))):
            # Stop at next block
            if j > i and lines[j].startswith('--- !u!'):
                break
            # Fix InterruptionSource: 1 -> 0
            if '  m_InterruptionSource: 1' in lines[j]:
                lines[j] = '  m_InterruptionSource: 0\n'
                fixes += 1
            # Fix CanTransitionToSelf: 1 -> 0
            if '  m_CanTransitionToSelf: 1' in lines[j]:
                lines[j] = '  m_CanTransitionToSelf: 0\n'
                fixes += 1
            # Fix TransitionDuration to 0 (instant)
            if '  m_TransitionDuration:' in lines[j]:
                lines[j] = '  m_TransitionDuration: 0\n'
                fixes += 1
    i += 1

with open(F, 'w', encoding='utf-8') as f:
    f.writelines(lines)

print(f"Fixed {fixes} field values across AnyState Exit transitions")
print(f"Output: {len(lines)} lines")

# Verify: check one
with open(F, 'r', encoding='utf-8') as f:
    content = f.read()
block = re.search(r'--- !u!1101 &9202133\b(.*?)--- !u!', content, re.DOTALL)
if block:
    print('\nVerify &9202133:')
    for line in block.group(1).splitlines():
        if any(k in line for k in ['InterruptionSource','CanTransitionToSelf','TransitionDuration']):
            print(' ', line)
