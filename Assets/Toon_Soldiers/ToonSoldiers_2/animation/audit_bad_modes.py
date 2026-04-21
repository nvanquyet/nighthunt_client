import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F, encoding='utf-8') as f:
    content = f.read()

# Find ALL transitions (1101) that use InteractIndex or AttackIndex with mode=1 (trigger mode)
pattern = re.compile(r'--- !u!1101 &(\d+)\n(.*?)(?=--- !u!)', content, re.DOTALL) if False else None

bad = []
for m in re.finditer(r'--- !u!1101 &(\d+)\n(.*?)(?=--- !u!)', content, re.DOTALL):
    fid = int(m.group(1))
    block = m.group(2)
    # Parse conditions (list format)
    cond_entries = re.findall(r'm_ConditionMode: (\d+)\s+m_ConditionEvent: (\S+)\s+m_EventTreshold: (\S+)', block)
    for mode, ev, thresh in cond_entries:
        if ev in ('InteractIndex', 'AttackIndex') and mode == '1':
            dst = re.search(r'm_DstState: \{fileID: (\d+)\}', block)
            bad.append((fid, ev, mode, thresh, dst.group(1) if dst else '?'))

print(f'BAD transitions (Int param with trigger mode): {len(bad)}')
for b in bad:
    print(f'  &{b[0]}: param={b[1]} mode={b[2]} thresh={b[3]} dst={b[4]}')

# Now find which UB_Empty state contains these transitions
if bad:
    bad_fids = {str(b[0]) for b in bad}
    states = re.findall(r'--- !u!1102 &(\d+)\n(.*?)(?=--- !u!)', content, re.DOTALL)
    ub_empty_states = [(fid, block) for fid, block in states if 'm_Name: UB_Empty' in block]
    for sfid, block in ub_empty_states:
        tm = re.search(r'm_Transitions:(.*?)m_StateMachineBehaviours:', block, re.DOTALL)
        if not tm: continue
        trans_fids = set(re.findall(r'fileID: (\d+)', tm.group(1)))
        overlap = trans_fids & bad_fids
        if overlap:
            print(f'\n  UB_Empty &{sfid} contains bad transitions: {overlap}')
