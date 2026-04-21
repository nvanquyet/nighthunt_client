"""
Fix m_ConditionMode for Trigger parameters.
Trigger params must use m_ConditionMode: 1 (If), not 9 (NotEqual).
"""

import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'

TRIGGER_PARAMS = {
    'Shoot', 'ShootBurst', 'Reload', 'Draw', 'ThrowGrenade',
    'Interact', 'TakeDamage', 'Attack', 'Die', 'Roll'
}

with open(F, 'r', encoding='utf-8') as f:
    lines = f.readlines()

print(f"Total lines: {len(lines)}")

fixed = 0
i = 0
while i < len(lines):
    # Look for "  - m_ConditionMode: 9" followed by m_ConditionEvent: <trigger>
    m0 = re.match(r'^(\s+)- m_ConditionMode: 9\s*$', lines[i])
    if m0:
        # Next line should be m_ConditionEvent
        if i + 1 < len(lines):
            ce_line = lines[i + 1]
            m = re.match(r'\s+m_ConditionEvent: (\w+)', ce_line)
            if m and m.group(1) in TRIGGER_PARAMS:
                # Fix: change mode 9 -> 1
                indent = m0.group(1)
                lines[i] = f'{indent}- m_ConditionMode: 1\n'
                fixed += 1
                print(f"  Fixed L{i+1}: {m.group(1)} condition mode 9->1")
    i += 1

print(f"\nTotal fixed: {fixed}")

with open(F, 'w', encoding='utf-8') as f:
    f.writelines(lines)

print(f"Done. Final lines: {len(lines)}")
