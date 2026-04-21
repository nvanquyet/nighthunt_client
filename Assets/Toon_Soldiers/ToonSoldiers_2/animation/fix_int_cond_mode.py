"""
Fix ConditionMode 5->6 for Int params in 1101 state-to-state transitions.
Mode 5 does NOT exist in Unity. Equals for Int = 6.
"""
import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
INT_PARAMS = {'WeaponType', 'DeathIndex', 'InteractIndex', 'AttackIndex'}

with open(F, encoding='utf-8') as f:
    content = f.read()

fixed = 0
blocks = list(re.finditer(r'(--- !u!1101 &\d+\n)(.*?)(?=--- !u!)', content, re.DOTALL))

result_parts = []
prev_end = 0

for m in blocks:
    header = m.group(1)
    body = m.group(2)
    new_body = body

    # Find conditions with mode 5 and Int params, fix to 6
    def fix_cond(cm):
        mode = cm.group(1)
        ev = cm.group(2)
        if mode == '5' and ev in INT_PARAMS:
            return cm.group(0).replace('m_ConditionMode: 5', 'm_ConditionMode: 6', 1)
        return cm.group(0)

    new_body2 = re.sub(
        r'm_ConditionMode: (\d+)\s+m_ConditionEvent: (\S+)\s+m_EventTreshold: \S+',
        fix_cond,
        new_body
    )

    if new_body2 != new_body:
        count = new_body.count('m_ConditionMode: 5')
        fixed += count
        new_body = new_body2

    result_parts.append(content[prev_end:m.start()])
    result_parts.append(header + new_body)
    prev_end = m.end()

result_parts.append(content[prev_end:])
new_content = ''.join(result_parts)

print(f'Fixed {fixed} ConditionMode 5->6 in 1101 transitions (Int params)')

# Verify no more mode 5 for Int params in 1101
remaining = []
for m in re.finditer(r'--- !u!1101 &(\d+)\n(.*?)(?=--- !u!)', new_content, re.DOTALL):
    body = m.group(2)
    for cm in re.finditer(r'm_ConditionMode: (\d+)\s+m_ConditionEvent: (\S+)', body):
        if cm.group(1) == '5' and cm.group(2) in INT_PARAMS:
            remaining.append((m.group(1), cm.group(2)))
if remaining:
    print(f'STILL BAD: {remaining}')
else:
    print('Verification: No more mode=5 for Int params in 1101 transitions.')

with open(F, 'w', encoding='utf-8') as f:
    f.write(new_content)
print('File saved.')
