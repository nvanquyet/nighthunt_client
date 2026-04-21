import re
F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F,'r',encoding='utf-8') as f: lines=f.readlines()

# Check Handgun_Death AnyState blocks (pre-existing)
print('=== Handgun_Death AnyState blocks (pre-existing) ===')
for fid in ['9200114', '9200116', '9200118']:
    for i,l in enumerate(lines):
        if re.match(r'^--- !u!\d+ &' + fid + r'\b', l):
            m = re.match(r'^--- !u!(\d+)', l)
            print(f'Block &{fid}: type={m.group(1)}')
            for jj in range(i, min(i+22, len(lines))):
                if jj>i and re.match(r'^--- !u!', lines[jj]): break
                print(f'  {lines[jj]}', end='')
            print()
            break

print()
print('=== Our new block (9200793) ===')
for i,l in enumerate(lines):
    if re.match(r'^--- !u!\d+ &9200793\b', l):
        m = re.match(r'^--- !u!(\d+)', l)
        print(f'type={m.group(1)}')
        for jj in range(i, min(i+22, len(lines))):
            if jj>i and re.match(r'^--- !u!', lines[jj]): break
            print(f'  {lines[jj]}', end='')
        break
