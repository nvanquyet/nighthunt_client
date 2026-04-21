import re
F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F,'r',encoding='utf-8') as f: lines=f.readlines()
print('Lines:', len(lines))

# Death root AnyState
for i,l in enumerate(lines):
    if re.match(r'^--- !u!1107 &9200784\b', l):
        for j in range(i+1, i+60):
            if 'm_AnyStateTransitions' in lines[j]:
                print('Death root AnyState:', lines[j].rstrip())
                break
        break

# Unarmed machines
for fid,name in [(9202000,'Unarmed_Base'),(9202010,'Unarmed_UpperBody'),(9202020,'Unarmed_Death')]:
    found = any(re.match(rf'^--- !u!1107 &{fid}\b', l) for l in lines)
    status = 'FOUND' if found else 'MISSING'
    print(f'{name}: {status}')

# Base sub-machine exits
BASE = [(9200001,'Handgun'),(9200126,'Infantry'),(9200257,'Heavy'),
        (9200391,'Knife'),(9200522,'Machinegun'),(9200656,'RocketLauncher')]
for fid,name in BASE:
    for i,l in enumerate(lines):
        if re.match(rf'^--- !u!1107 &{fid}\b', l):
            for j in range(i+1, i+50):
                if 'm_AnyStateTransitions' in lines[j]:
                    val = lines[j].rstrip()
                    status = 'OK' if '[]' not in val else 'EMPTY'
                    print(f'{name}_Base AnyState: {status} -- {val}')
                    break
            break
