import re
F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F,'r',encoding='utf-8') as f: lines=f.readlines()

# Find AnimatorController block
for i,l in enumerate(lines):
    if re.match(r'^--- !u!91 &', l):
        print(f'AnimatorController at L{i+1}')
        j = i+1
        while j < len(lines) and not re.match(r'^--- !u!', lines[j]):
            print(f'  L{j+1}: {lines[j]}', end='')
            j += 1
            if j > i + 80:
                print('  ...(truncated)')
                break
        break

# Find root StateMachine for UpperBody layer
# Layers are referenced by their StateMachine fileID
# Find all !u!1107 machines and check which is root UpperBody
print()
print('=== All !u!1107 machines (top-level only, no parent) ===')
machines = []
i = 0
while i < len(lines):
    if re.match(r'^--- !u!1107 &', lines[i]):
        fid = re.search(r'&(\d+)', lines[i]).group(1)
        name = ''
        j = i+1
        while j < len(lines) and not re.match(r'^--- !u!', lines[j]):
            if re.match(r'\s+m_Name:', lines[j]) and not name:
                name = lines[j].split('m_Name:',1)[1].strip()
            j += 1
        machines.append((fid, name))
    i += 1

for fid, name in machines:
    print(f'  &{fid}: {repr(name)}')
