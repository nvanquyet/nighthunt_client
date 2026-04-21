import re
with open('SoldierAnimatorController.controller','r',encoding='utf-8') as f: c=f.read()

DEATH_SMs = [9200111, 9200243, 9200378, 9200510, 9200645, 9200762, 9202220]
names     = ['Handgun','Infantry','Heavy','Knife','Machinegun','RL','Unarmed']

print('=== Death sub-machine AnyState transitions ===')
for fid, name in zip(DEATH_SMs, names):
    block = re.search(rf'--- !u!\d+ &{fid}\b(.*?)--- !u!', c, re.DOTALL)
    if not block: continue
    any_section = re.search(r'm_AnyStateTransitions:(.*?)m_EntryTransitions:', block.group(1), re.DOTALL)
    refs = re.findall(r'fileID: (\d+)', any_section.group(1)) if any_section else []
    print(f'{name}_Death (SM:{fid}): {len(refs)} AnyState refs -> {refs}')

print()
print('=== Death ROOT (9200850) AnyState ===')
root = re.search(r'--- !u!\d+ &9200850\b(.*?)--- !u!', c, re.DOTALL)
if root:
    any_s = re.search(r'm_AnyStateTransitions:(.*?)m_EntryTransitions:', root.group(1), re.DOTALL)
    refs = re.findall(r'fileID: (\d+)', any_s.group(1)) if any_s else []
    print(f'Root AnyState refs: {refs}')
    for ref in refs:
        tb = re.search(rf'--- !u!\d+ &{ref}\b(.*?)--- !u!', c, re.DOTALL)
        if tb:
            cond = re.search(r'm_ConditionEvent: (\S+)', tb.group(1))
            cname = cond.group(1) if cond else 'none'
            print(f'  &{ref}: cond={cname}')

print()
print('=== Check WeaponChanged usage in Death layer ===')
# Count WeaponChanged / WeaponChangedUB references
wc = len(re.findall(r'WeaponChanged\b', c))
wcub = len(re.findall(r'WeaponChangedUB\b', c))
print(f'WeaponChanged total refs: {wc}')
print(f'WeaponChangedUB total refs: {wcub}')
