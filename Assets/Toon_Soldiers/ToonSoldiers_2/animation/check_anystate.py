import re
F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F,'r',encoding='utf-8') as f: lines=f.readlines()

target_fids = ['9200001','9200126','9200257','9200391','9200522','9200656',
               '9200048','9200173','9200304','9200438','9200569','9200703']
for fid in target_fids:
    for i,l in enumerate(lines):
        if re.match(rf'^--- !u!1107 &{fid}\b', l):
            name = ''
            for j in range(i+1, i+10):
                if 'm_Name' in lines[j]:
                    name = lines[j].strip()
                    break
            for j in range(i+1, i+50):
                if 'm_AnyStateTransitions' in lines[j]:
                    ctx = lines[j].rstrip()
                    nxt = lines[j+1].rstrip() if j+1 < len(lines) else ''
                    print(f'{fid} {name}:')
                    print(f'  {ctx}')
                    print(f'  {nxt}')
                    break
