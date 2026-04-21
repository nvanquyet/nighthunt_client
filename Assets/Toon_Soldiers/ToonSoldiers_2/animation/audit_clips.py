"""
Audit motion (clip) GUIDs for Shoot/Draw/Reload states in each UpperBody sub-machine.
Cross-check against .meta files to identify which clip each state is actually using.
"""
import re, os

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
ANIM_DIR = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation'

with open(F, encoding='utf-8') as f:
    content = f.read()

# Build GUID->filename map from .meta files
guid_map = {}
for root, dirs, files in os.walk(r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers'):
    for fn in files:
        if fn.endswith('.meta'):
            fp = os.path.join(root, fn)
            try:
                text = open(fp, encoding='utf-8').read()
                m = re.search(r'^guid: ([a-f0-9]+)', text, re.MULTILINE)
                if m:
                    guid_map[m.group(1)] = fn.replace('.meta', '')
            except: pass

print(f'Loaded {len(guid_map)} GUIDs from meta files\n')

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

# Get all states in a sub-machine
def get_sm_states(sm_fid):
    sm_block_m = re.search(rf'--- !u!\d+ &{sm_fid}\b(.*?)(?=--- !u!)', content, re.DOTALL)
    if not sm_block_m:
        return []
    sm_block = sm_block_m.group(1)
    # Get m_States list
    states_m = re.search(r'm_States:(.*?)m_AnyStateTransitions:', sm_block, re.DOTALL)
    if not states_m:
        return []
    return re.findall(r'fileID: (\d+)', states_m.group(1))

# Get state name and motion GUID
def get_state_info(state_fid):
    sb = re.search(rf'--- !u!1102 &{state_fid}\b(.*?)(?=--- !u!)', content, re.DOTALL)
    if not sb:
        return None, None, None
    block = sb.group(1)
    name_m = re.search(r'm_Name: (.+)', block)
    motion_m = re.search(r'm_Motion: \{fileID: (\d+), guid: ([a-f0-9]+)', block)
    name = name_m.group(1).strip() if name_m else '?'
    if motion_m:
        guid = motion_m.group(2)
        clip_name = guid_map.get(guid, f'UNKNOWN_GUID_{guid}')
        return name, guid, clip_name
    return name, None, None

# Interesting state names to check
INTERESTING = {'Shoot_Stand', 'Shoot_Crouch', 'Shoot_Prone', 'Draw_Stand',
               'Reload_Stand', 'ShootBurst_Stand', 'Shoot_Loop_Stand',
               'Shoot_Bolt_Stand', 'Shoot_Shotgun_Stand', 'UB_Empty'}

print('=' * 70)
for sm_name, sm_fid in UB_SMS.items():
    state_fids = get_sm_states(sm_fid)
    print(f'\n{sm_name} (FID={sm_fid}): {len(state_fids)} states')
    # Filter to interesting states
    for sfid in state_fids:
        name, guid, clip_name = get_state_info(sfid)
        if name and any(k in name for k in INTERESTING):
            if guid:
                match = '✓' if sm_name.split('_')[0].lower() in (clip_name or '').lower() else '?'
                print(f'  [{match}] {name:30s} -> {clip_name}')
            else:
                print(f'  [!] {name:30s} -> NO MOTION (null clip!)')
