import re
F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F,'r',encoding='utf-8') as f: content=f.read()

def get_anystate_refs(machine_fid):
    block = re.search(rf'--- !u!\d+ &{machine_fid}\b(.*?)m_StateMachineTransitions:', content, re.DOTALL)
    if not block: return []
    any_block = re.search(r'm_AnyStateTransitions:(.*?)m_EntryTransitions:', block.group(1), re.DOTALL)
    if not any_block: return []
    return re.findall(r'fileID: (\d+)', any_block.group(1))

# Check all UB sub-machines
UB_MACHINES = [
    (9200048, 'Handgun_UB'),
    (9200174, 'Infantry_UB'),
    (9200306, 'Heavy_UB'),
    (9200441, 'Knife_UB'),
    (9200573, 'Machinegun_UB'),
    (9200708, 'RL_UB'),
    (9202210, 'Unarmed_UB'),
]
for fid, name in UB_MACHINES:
    refs = get_anystate_refs(fid)
    print(f'{name}: {len(refs)} AnyState transitions')

print()
# Show all transitions for Handgun_UB
refs = get_anystate_refs(9200048)
print(f'Handgun_UB detailed ({len(refs)} total):')
for ref in refs:
    tb = re.search(rf'--- !u!1101 &{ref}\b(.*?)--- !u!', content, re.DOTALL)
    if tb:
        conds = re.findall(r'm_ConditionEvent: (\S+)', tb.group(1))
        dst_m = re.search(r'm_DstState: \{fileID: (\d+)\}', tb.group(1))
        isexit = re.search(r'm_IsExit: (\d)', tb.group(1))
        dst = dst_m.group(1) if dst_m else 'none'
        ex = isexit.group(1) if isexit else '?'
        print(f'  &{ref}: {conds} -> state:{dst} exit:{ex}')
