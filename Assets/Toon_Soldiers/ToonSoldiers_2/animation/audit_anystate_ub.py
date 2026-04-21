"""
Audit AnyState transitions in UpperBody root and in each UB sub-machine.
Check if root AnyState is pointing directly to Handgun states (wrong).
"""
import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F, encoding='utf-8') as f:
    content = f.read()

# Known FID ranges per weapon sub-machine (UB)
UB_FID_RANGES = {
    'Unarmed_UB':        (9202210, 9202219),
    'Handgun_UB':        (9200048, 9200126),
    'Infantry_UB':       (9200174, 9200258),
    'Heavy_UB':          (9200306, 9200393),
    'Knife_UB':          (9200441, 9200525),
    'Machinegun_UB':     (9200573, 9200660),
    'RocketLauncher_UB': (9200708, 9200773),
}

def fid_to_weapon(fid):
    fid = int(fid)
    for name, (lo, hi) in UB_FID_RANGES.items():
        if lo <= fid <= hi:
            return name
    return f'UNKNOWN({fid})'

def get_sm_anystate_trans(sm_fid, sm_name):
    sm_m = re.search(rf'--- !u!\d+ &{sm_fid}\b(.*?)(?=--- !u!)', content, re.DOTALL)
    if not sm_m:
        return
    block = sm_m.group(1)
    any_m = re.search(r'm_AnyStateTransitions:(.*?)m_EntryTransitions:', block, re.DOTALL)
    if not any_m:
        return
    trans_fids = re.findall(r'fileID: (\d+)', any_m.group(1))
    if not trans_fids:
        return

    print(f'\n{sm_name} (FID={sm_fid}): {len(trans_fids)} AnyState transitions')
    for tfid in trans_fids:
        tb = re.search(rf'--- !u!1101 &{tfid}\b(.*?)(?=--- !u!)', content, re.DOTALL)
        if not tb:
            print(f'  &{tfid}: NOT FOUND')
            continue
        tblock = tb.group(1)
        dst = re.search(r'm_DstState: \{fileID: (\d+)\}', tblock)
        mute = re.search(r'm_Mute: (\d+)', tblock)
        is_exit = re.search(r'm_IsExit: (\d+)', tblock)
        conds = re.findall(r'm_ConditionEvent: (\S+)\s+m_ConditionMode: (\d+)\s+m_EventTreshold: (\S+)', tblock)
        
        dst_fid = dst.group(1) if dst else '?'
        dst_weapon = fid_to_weapon(dst_fid) if dst_fid != '?' else '?'
        muted = mute.group(1) == '1' if mute else False
        is_exit_val = is_exit.group(1) if is_exit else '?'
        cond_str = ', '.join(f'{e}({m},{t})' for e, m, t in conds)
        
        status = '[MUTED]' if muted else '[ACTIVE]'
        dst_info = f'Exit' if is_exit_val == '1' else f'dst={dst_fid}({dst_weapon})'
        print(f'  {status} &{tfid}: {cond_str} -> {dst_info}')

# Check root UpperBody (9200812)
print('=== ROOT UpperBody (9200812) ===')
get_sm_anystate_trans(9200812, 'UpperBody ROOT')

# Check each UB sub-machine
UB_SMS = {
    'Unarmed_UB':        9202210,
    'Handgun_UB':        9200048,
    'Infantry_UB':       9200174,
    'Heavy_UB':          9200306,
    'Knife_UB':          9200441,
    'Machinegun_UB':     9200573,
    'RocketLauncher_UB': 9200708,
}
print('\n=== UB Sub-machines ===')
for name, fid in UB_SMS.items():
    get_sm_anystate_trans(fid, name)
