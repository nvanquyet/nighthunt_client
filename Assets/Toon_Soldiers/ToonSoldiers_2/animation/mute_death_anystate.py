"""
Mute all old AnyState transitions inside Death sub-machines.
These are legacy broken transitions (DeathIndex mode=6=NotEqual, wrong logic).
New correct transitions are on Death_Empty state (added by fix_death_layer.py).

AnyState FIDs to mute per weapon:
Handgun:    9200114, 9200116, 9200118, 9200120, 9200122, 9200124, 9200125
Infantry:   9200246, 9200248, 9200250, 9200252, 9200254, 9200256, 9200257
Heavy:      9200381, 9200383, 9200385, 9200387, 9200389, 9200391, 9200392
Knife:      9200513, 9200515, 9200517, 9200519, 9200521, 9200523, 9200524
Machinegun: 9200648, 9200650, 9200652, 9200654, 9200656, 9200658, 9200659
RL:         9200765, 9200767, 9200769, 9200771, 9200772
Unarmed:    9202228, 9202229, 9202230, 9202231, 9202232, 9202233, 9202234
"""
import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F, 'r', encoding='utf-8') as f:
    content = f.read()

MUTE_FIDS = [
    9200114, 9200116, 9200118, 9200120, 9200122, 9200124, 9200125,
    9200246, 9200248, 9200250, 9200252, 9200254, 9200256, 9200257,
    9200381, 9200383, 9200385, 9200387, 9200389, 9200391, 9200392,
    9200513, 9200515, 9200517, 9200519, 9200521, 9200523, 9200524,
    9200648, 9200650, 9200652, 9200654, 9200656, 9200658, 9200659,
    9200765, 9200767, 9200769, 9200771, 9200772,
    9202228, 9202229, 9202230, 9202231, 9202232, 9202233, 9202234,
]

muted = 0
already = 0

for fid in MUTE_FIDS:
    m = re.search(rf'(--- !u!1101 &{fid}\b.*?)(--- !u!)', content, re.DOTALL)
    if not m:
        print(f'WARNING: &{fid} not found')
        continue
    block = m.group(1)
    if '  m_Mute: 1' in block:
        already += 1
        continue
    new_block = block.replace('  m_Mute: 0\n', '  m_Mute: 1\n', 1)
    content = content[:m.start()] + new_block + content[m.start()+len(block):]
    muted += 1

print(f'Muted: {muted}')
print(f'Already muted: {already}')
print(f'Total: {muted + already}/{len(MUTE_FIDS)}')

with open(F, 'w', encoding='utf-8') as f:
    f.write(content)
print('File saved.')
