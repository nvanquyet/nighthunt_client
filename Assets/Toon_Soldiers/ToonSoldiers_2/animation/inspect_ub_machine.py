import re
F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F,'r',encoding='utf-8') as f: lines=f.readlines()

# Print entire UpperBody root machine block (&9200776)
for i,l in enumerate(lines):
    if re.match(r'^--- !u!1107 &9200776\b', l):
        print(f'UpperBody root machine at L{i+1}')
        j = i+1
        while j < len(lines) and not re.match(r'^--- !u!', lines[j]):
            print(f'  L{j+1}: {lines[j]}', end='')
            j += 1
        break

# Also check the m_Layers in AnimatorController to see UpperBody layer config
print()
print('=== AnimatorController m_Layers ===')
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
