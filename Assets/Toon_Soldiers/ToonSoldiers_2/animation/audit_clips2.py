"""
Audit motion GUIDs for all UB sub-machine states, map to actual clip filenames.
"""
import re, os

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'

with open(F, encoding='utf-8') as f:
    content = f.read()

# Build GUID->filename map from .meta files
guid_map = {}
for root_dir, dirs, files in os.walk(r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers'):
    for fn in files:
        if fn.endswith('.meta'):
            fp = os.path.join(root_dir, fn)
            try:
                text = open(fp, encoding='utf-8').read()
                m = re.search(r'^guid: ([a-f0-9]+)', text, re.MULTILINE)
                if m:
                    guid_map[m.group(1)] = fn.replace('.meta', '')
            except:
                pass

print(f'Loaded {len(guid_map)} GUIDs\n')

# UB sub-machines: name -> FID
UB_SMS = {
    'Unarmed_UB':        9202210,
    'Handgun_UB':        9200048,
    'Infantry_UB':       9200174,
    'Heavy_UB':          9200306,
    'Knife_UB':          9200441,
    'Machinegun_UB':     9200573,
    'RocketLauncher_UB': 9200708,
}

def get_sm_child_fids(sm_fid):
    sm_m = re.search(rf'--- !u!\d+ &{sm_fid}\b(.*?)(?=--- !u!)', content, re.DOTALL)
    if not sm_m:
        return []
    block = sm_m.group(1)
    cs_m = re.search(r'm_ChildStates:(.*?)m_ChildStateMachines:', block, re.DOTALL)
    if not cs_m:
        return []
    return re.findall(r'm_State: \{fileID: (\d+)\}', cs_m.group(1))

def get_state_info(fid):
    sb = re.search(rf'--- !u!1102 &{fid}\b(.*?)(?=--- !u!)', content, re.DOTALL)
    if not sb:
        return '?', None, '?'
    block = sb.group(1)
    name = re.search(r'm_Name: (.+)', block)
    name = name.group(1).strip() if name else '?'
    motion = re.search(r'm_Motion: \{fileID: \d+, guid: ([a-f0-9]+)', block)
    if motion:
        guid = motion.group(1)
        clip = guid_map.get(guid, f'??UNKNOWN {guid[:8]}')
        return name, guid, clip
    return name, None, '(null motion)'

INTERESTING = {'Shoot', 'Draw', 'Reload', 'UB_Empty', 'TakeDamage', 'Guard', 'Grenade', 'Attack'}

print(f'{"SM":<20} {"State":<28} {"Clip filename"}')
print('-' * 90)

for sm_name, sm_fid in UB_SMS.items():
    fids = get_sm_child_fids(sm_fid)
    for fid in fids:
        state_name, guid, clip = get_state_info(fid)
        if any(k in state_name for k in INTERESTING):
            flag = ''
            if guid:
                # Check if clip name matches expected weapon name
                weapon = sm_name.split('_')[0].lower()
                if weapon not in clip.lower() and 'UNKNOWN' not in clip:
                    flag = '  ← MISMATCH!'
            else:
                flag = '  ← NULL CLIP!'
            print(f'{sm_name:<20} {state_name:<28} {clip}{flag}')
    print()
