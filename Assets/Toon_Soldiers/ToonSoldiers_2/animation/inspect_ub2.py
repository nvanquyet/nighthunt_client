import re
F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F,'r',encoding='utf-8') as f: lines=f.readlines()

# 1. Check Entry transitions 9200777-9200783
print('=== Entry transitions of root UpperBody ===')
for fid in ['9200777','9200778','9200779','9200780','9200781','9200782','9200783']:
    for i,l in enumerate(lines):
        if re.match(r'^--- !u!\d+ &' + fid + r'\b', l):
            block_type = re.match(r'^--- !u!(\d+)', l).group(1)
            dst_state = ''
            dst_machine = ''
            conditions = []
            j = i+1
            while j < len(lines) and not re.match(r'^--- !u!', lines[j]):
                if 'm_DstState:' in lines[j]:
                    dst_state = lines[j].strip()
                if 'm_DstStateMachine:' in lines[j]:
                    dst_machine = lines[j].strip()
                if 'm_ConditionEvent:' in lines[j]:
                    conditions.append(lines[j].strip())
                if 'm_ConditionMode:' in lines[j]:
                    conditions.append(lines[j].strip())
                j += 1
            print(f'  &{fid} type={block_type}: {conditions} -> {dst_machine} {dst_state}')
            break

# 2. Check AnimatorController layers
print()
print('=== m_Layers ===')
for i,l in enumerate(lines):
    if re.match(r'^--- !u!91 &', l):
        j = i+1
        in_layers = False
        while j < len(lines) and not re.match(r'^--- !u!', lines[j]):
            if 'm_Layers:' in lines[j]:
                in_layers = True
            if in_layers:
                print(f'  L{j+1}: {lines[j]}', end='')
            j += 1
        break

# 3. Check what 9200049 is
print()
print('=== State &9200049 ===')
for i,l in enumerate(lines):
    if re.match(r'^--- !u!\d+ &9200049\b', l):
        for j in range(i, min(i+12, len(lines))):
            if j>i and re.match(r'^--- !u!', lines[j]): break
            print(f'  {lines[j]}', end='')
        break
