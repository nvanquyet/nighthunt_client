import re
F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F,'r',encoding='utf-8') as f: content=f.read()

# Check Handgun_UB action states for transitions
STATES = {
    'UB_Empty':      9200049,
    'Draw_Stand':    9200050,
    'Shoot_Stand':   9200052,
    'Reload_Stand':  9200054,
    'Grenade_Stand': 9200056,
    'Interact_A':    9200058,
    'Interact_B':    9200060,
    'Damage_Stand':  9200062,
    'ShootBurst_Stand': 9200064,
}

for name, fid in STATES.items():
    sb = re.search(rf'--- !u!1102 &{fid}\b(.*?)--- !u!', content, re.DOTALL)
    if not sb:
        print(f'{name} ({fid}): NOT FOUND')
        continue
    trans = re.findall(r'fileID: (\d+)', re.search(r'm_Transitions:(.*?)m_StateMachineBehaviours:', sb.group(1), re.DOTALL).group(1) if 'm_Transitions:' in sb.group(1) else '')
    has_exit = re.search(r'm_IsExit: 1', sb.group(1))
    
    # Check each transition ref
    exit_info = []
    for t in trans:
        tb = re.search(rf'--- !u!1101 &{t}\b(.*?)--- !u!', content, re.DOTALL)
        if tb:
            is_exit = re.search(r'm_IsExit: (\d)', tb.group(1))
            dst = re.search(r'm_DstState: \{fileID: (\d+)\}', tb.group(1))
            conds = re.findall(r'm_ConditionEvent: (\S+)', tb.group(1))
            has_exit_time = re.search(r'm_HasExitTime: (\d)', tb.group(1))
            exit_info.append(f'  -> t&{t}: IsExit={is_exit.group(1) if is_exit else "?"} dst={dst.group(1) if dst else "0"} conds={conds} exitTime={has_exit_time.group(1) if has_exit_time else "?"}')
    
    if exit_info:
        print(f'{name} ({fid}): {len(exit_info)} transitions')
        for e in exit_info:
            print(e)
    else:
        print(f'{name} ({fid}): NO TRANSITIONS (m_Transitions empty)')
