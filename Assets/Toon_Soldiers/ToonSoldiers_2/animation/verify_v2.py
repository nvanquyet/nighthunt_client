import re
F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F,'r',encoding='utf-8') as f: lines=f.readlines()
print(f'Total lines: {len(lines)}')

TRIGGERS = {'Shoot','ShootBurst','Reload','Draw','ThrowGrenade','Interact','TakeDamage','Attack','Die','Roll','Respawn'}
remaining9 = []
for i,l in enumerate(lines):
    if '  - m_ConditionMode: 9' in l:
        if i+1 < len(lines) and '    m_ConditionEvent:' in lines[i+1]:
            event = lines[i+1].split(':',1)[1].strip()
            if event in TRIGGERS:
                remaining9.append((i+1, event))
print(f'[1] Remaining mode=9 trigger conditions: {len(remaining9)}')
if remaining9: print('    Examples:', remaining9[:3])

BASE = [(9200001,'Handgun_Base'),(9200127,'Infantry_Base'),(9200259,'Heavy_Base'),
        (9200394,'Knife_Base'),(9200526,'Machinegun_Base'),(9200661,'RL_Base')]
print('\n[2] Base Layer AnyState transitions:')
for mfid, name in BASE:
    pat = re.compile(rf'^--- !u!\d+ &{mfid}\b')
    for i,l in enumerate(lines):
        if pat.match(l):
            for j in range(i,i+25):
                if 'm_AnyStateTransitions:' in lines[j]:
                    count = sum(1 for k in range(j+1,j+10) if '- {fileID:' in lines[k])
                    print(f'    {name}: AnyState={count}')
                    break
            break

UB = [(9200048,'Handgun_UB'),(9200174,'Infantry_UB'),(9200306,'Heavy_UB'),
      (9200441,'Knife_UB'),(9200573,'Machinegun_UB'),(9200708,'RL_UB')]
print('\n[3] UpperBody AnyState transitions:')
for mfid, name in UB:
    pat = re.compile(rf'^--- !u!\d+ &{mfid}\b')
    for i,l in enumerate(lines):
        if pat.match(l):
            for j in range(i,i+30):
                if 'm_AnyStateTransitions:' in lines[j]:
                    count = sum(1 for k in range(j+1,j+60) if '- {fileID:' in lines[k])
                    print(f'    {name}: AnyState={count}')
                    break
            break

print('\n[4] Unarmed sub-machines:')
for name in ['Unarmed_Base','Unarmed_UpperBody','Unarmed_Death']:
    found = any(f'  m_Name: {name}' in l for l in lines)
    status = 'OK' if found else 'MISSING'
    print(f'    {name}: {status}')

die_mode1 = sum(1 for i,l in enumerate(lines) if '  - m_ConditionMode: 1' in l and i+1<len(lines) and 'm_ConditionEvent: Die' in lines[i+1])
die_mode9 = sum(1 for i,l in enumerate(lines) if '  - m_ConditionMode: 9' in l and i+1<len(lines) and 'm_ConditionEvent: Die' in lines[i+1])
print(f'\n[5] Die trigger: mode=1={die_mode1}, mode=9(bad)={die_mode9}')

# Check root entry transitions for all 3 layers
for root_fid, rname in [(9200774,'Base_root'),(9200812,'UB_root'),(9200850,'Death_root')]:
    pat = re.compile(rf'^--- !u!\d+ &{root_fid}\b')
    for i,l in enumerate(lines):
        if pat.match(l):
            for j in range(i,i+30):
                if 'm_EntryTransitions:' in lines[j]:
                    count = sum(1 for k in range(j+1,j+15) if '- {fileID:' in lines[k])
                    print(f'[6] {rname} entry transitions: {count}')
                    break
            break
