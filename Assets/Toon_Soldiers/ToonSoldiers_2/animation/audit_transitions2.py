import re

path = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
lines = open(path, encoding='utf-8').readlines()

# Build maps
state_names = {}
state_speeds = {}
cur_fid = None
for i, l in enumerate(lines):
    m = re.match(r'--- !u!1102 &(\d+)', l)
    if m:
        cur_fid = m.group(1)
    if cur_fid:
        if 'm_Name:' in l and state_names.get(cur_fid) is None:
            state_names[cur_fid] = l.strip().replace('m_Name: ', '').strip()
        if 'm_Speed:' in l and state_speeds.get(cur_fid) is None:
            state_speeds[cur_fid] = l.strip().replace('m_Speed: ', '').strip()

sm_names = {}
cur_fid = None
for i, l in enumerate(lines):
    m = re.match(r'--- !u!1107 &(\d+)', l)
    if m:
        cur_fid = m.group(1)
    if cur_fid and 'm_Name:' in l and sm_names.get(cur_fid) is None:
        sm_names[cur_fid] = l.strip().replace('m_Name: ', '').strip()

# Build SM->states map (parent SM for each state)
sm_states = {}  # sm_fid -> [state_fids]
cur_sm = None
for i, l in enumerate(lines):
    ms = re.match(r'--- !u!1107 &(\d+)', l)
    if ms:
        cur_sm = ms.group(1)
        sm_states[cur_sm] = []
    if cur_sm:
        ref = re.search(r'- \{fileID: (\d+)\}', l)
        if ref:
            fid = ref.group(1)
            if fid in state_names:
                sm_states[cur_sm].append(fid)

# Parse state FID -> list of transition FIDs
# by finding each 1102 block and reading m_Transitions
state_trans = {}  # state_fid -> [trans_fid]
cur_fid = None
in_trans = False
for i, l in enumerate(lines):
    m = re.match(r'--- !u!1102 &(\d+)', l)
    if m:
        cur_fid = m.group(1)
        in_trans = False
        state_trans[cur_fid] = []
    if cur_fid:
        if 'm_Transitions:' in l:
            in_trans = True
        elif in_trans:
            ref = re.search(r'- \{fileID: (\d+)\}', l)
            if ref:
                state_trans[cur_fid].append(ref.group(1))
            elif l.strip() and not l.strip().startswith('-') and 'm_' in l:
                in_trans = False

# Parse all 1101 blocks
results = []
for i, l in enumerate(lines):
    m = re.match(r'--- !u!1101 &(\d+)', l)
    if not m:
        continue
    fid = m.group(1)
    chunk = ''.join(lines[i:i+35])
    has_exit = re.search(r'm_HasExitTime: (\d)', chunk)
    exit_time = re.search(r'm_ExitTime: ([\d.eE+\-]+)', chunk)
    duration = re.search(r'm_TransitionDuration: ([\d.eE+\-]+)', chunk)
    offset = re.search(r'm_TransitionOffset: ([\d.eE+\-]+)', chunk)
    conditions_block = re.search(r'm_Conditions:(.*?)m_DstState:', chunk, re.DOTALL)
    dst_state = re.search(r'm_DstState: \{fileID: (\d+)\}', chunk)
    dst_sm = re.search(r'm_DstStateMachine: \{fileID: (\d+)\}', chunk)
    is_exit_flag = re.search(r'm_IsExit: (\d)', chunk)
    cond_evts = re.findall(r'm_ConditionEvent: (\S+)', chunk)
    cond_modes = re.findall(r'm_ConditionMode: (\d+)', chunk)
    cond_thrs = re.findall(r'm_EventTreshold: ([\d.eE+\-]+)', chunk)

    dst_name = ''
    if is_exit_flag and is_exit_flag.group(1) == '1':
        dst_name = '[Exit]'
    elif dst_state:
        dsf = dst_state.group(1)
        dst_name = state_names.get(dsf, 'fid:' + dsf) if dsf != '0' else '[Exit]'
    elif dst_sm:
        dsf = dst_sm.group(1)
        dst_name = '[SM:' + sm_names.get(dsf, 'fid:' + dsf) + ']' if dsf != '0' else '[Exit]'

    conds = list(zip(cond_evts, cond_modes, cond_thrs))
    results.append({
        'fid': fid,
        'has_exit': has_exit.group(1) if has_exit else '?',
        'exit_time': float(exit_time.group(1)) if exit_time else -1,
        'duration': float(duration.group(1)) if duration else -1,
        'offset': float(offset.group(1)) if offset else 0,
        'dst': dst_name,
        'conds': conds,
    })

trans_by_fid = {r['fid']: r for r in results}

# -- REPORT --

def show_state_transitions(name_filter):
    print()
    print(f'=== States matching "{name_filter}" ===')
    for sfid, sname in sorted(state_names.items(), key=lambda x: x[1]):
        if name_filter.lower() not in sname.lower():
            continue
        speed = state_speeds.get(sfid, '1')
        trans = state_trans.get(sfid, [])
        print(f'  State [{sfid}] {sname}  speed={speed}  ({len(trans)} transitions)')
        for tfid in trans:
            r = trans_by_fid.get(tfid)
            if r:
                cstr = ', '.join(f'{e}(mode={m},val={v})' for e, m, v in r['conds'])
                if not cstr:
                    cstr = '(no conditions)'
                print(f'    -> {r["dst"]}  hasExit={r["has_exit"]} exitT={r["exit_time"]:.3f} dur={r["duration"]:.3f}  [{cstr}]')

show_state_transitions('Draw')
show_state_transitions('Holster')
show_state_transitions('Shoot')
show_state_transitions('Attack')
show_state_transitions('UB_Empty')
show_state_transitions('Idle')
show_state_transitions('Reload')

print()
print('=== State speed != 1 ===')
for sfid, sp in sorted(state_speeds.items()):
    try:
        if abs(float(sp) - 1.0) > 0.001:
            print(f'  [{sfid}] {state_names.get(sfid,"?")}  speed={sp}')
    except:
        pass
