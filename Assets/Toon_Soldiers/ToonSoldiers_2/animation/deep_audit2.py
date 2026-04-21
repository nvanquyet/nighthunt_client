"""
deep_audit2.py - Check critical issues:
1. Entry transition order (unconditional before WT==6?)
2. UpperBody action states - do they have exit transitions?
3. Death states - check m_HasExitTime on transitions
4. Shooting types per sub-machine
"""
import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F,'r',encoding='utf-8') as f: lines=f.readlines()
print(f"Lines: {len(lines)}")

def find_block(fid):
    pat = re.compile(rf'^--- !u!\d+ &{fid}\b')
    for i,l in enumerate(lines):
        if pat.match(l):
            start=i
            for j in range(i+1, len(lines)):
                if lines[j].startswith('--- !u!'):
                    return (start,j)
            return (start, len(lines))
    return (-1,-1)

def get_field(lines, start, end, field):
    for i in range(start, end):
        if field in lines[i]:
            return lines[i].strip()
    return None

def get_transitions(lines, start, end):
    """Get m_Transitions list from a state block."""
    for i in range(start, end):
        if '  m_Transitions:' in lines[i]:
            if '[]' in lines[i]:
                return []
            result = []
            j = i+1
            while j < end and lines[j].strip().startswith('- {fileID:'):
                m = re.search(r'fileID: (\d+)', lines[j])
                if m: result.append(int(m.group(1)))
                j += 1
            return result
    return []

def get_entry_list(root_fid, root_name):
    start,end = find_block(root_fid)
    print(f"\n{'='*60}")
    print(f"ROOT: {root_name} &{root_fid}")
    print(f"{'='*60}")
    for i in range(start, end):
        if '  m_EntryTransitions:' in lines[i]:
            j = i+1
            while j < end and lines[j].strip().startswith('- {fileID:'):
                m = re.search(r'fileID: (\d+)', lines[j])
                if m:
                    tid = int(m.group(1))
                    ts, te = find_block(tid)
                    if ts >= 0:
                        conds = []
                        for k in range(ts,te):
                            if 'm_ConditionMode:' in lines[k]:
                                mode = int(lines[k].split(':')[1].strip())
                                ev = ''
                                thr = 0
                                if k+1 < te: ev = lines[k+1].split(':',1)[1].strip()
                                if k+2 < te: thr = lines[k+2].split(':',1)[1].strip()
                                conds.append(f"{ev}(mode={mode},thr={thr})")
                        dst_m = '?'
                        dst_s = '?'
                        for k in range(ts,te):
                            if 'm_DstStateMachine:' in lines[k]:
                                m2 = re.search(r'fileID: (\d+)', lines[k])
                                if m2: dst_m = m2.group(1)
                            if 'm_DstState:' in lines[k]:
                                m2 = re.search(r'fileID: (\d+)', lines[k])
                                if m2: dst_s = m2.group(1)
                        cond_str = ', '.join(conds) if conds else 'NO CONDITION'
                        print(f"  &{tid}: [{cond_str}] →machine:{dst_m} state:{dst_s}")
                j += 1
            break

# Check entry order for all 3 roots
get_entry_list(9200768, 'Base Layer')
get_entry_list(9200776, 'UpperBody')
get_entry_list(9200784, 'Death')

# ── Check UpperBody action states for exit transitions ──
print(f"\n{'='*60}")
print("UPPERBODY ACTION STATES - Exit transitions check")
print(f"{'='*60}")

# Handgun_UpperBody: check some action states
HG_UB_STATES_NAMES = {
    9200049: 'UB_Empty',
    9200050: 'Shoot_Stand(likely)',
    9200051: 'Reload_Stand(likely)',
}

# Find all states in Handgun_UpperBody (FID 9200048)
start, end = find_block(9200048)
child_state_fids = []
for i in range(start, end):
    if 'm_State: {fileID:' in lines[i]:
        m = re.search(r'fileID: (\d+)', lines[i])
        if m: child_state_fids.append(int(m.group(1)))

print(f"\nHandgun_UpperBody states ({len(child_state_fids)} total):")
for sfid in child_state_fids[:8]:  # check first 8
    ss, se = find_block(sfid)
    if ss < 0: continue
    name_line = get_field(lines, ss, se, 'm_Name:')
    name = name_line.split(':',1)[1].strip() if name_line else '?'
    transitions = get_transitions(lines, ss, se)
    # Check HasExitTime in each transition
    exit_details = []
    for tid in transitions:
        ts, te = find_block(tid)
        has_exit_time = get_field(lines, ts, te, 'm_HasExitTime:')
        is_exit = get_field(lines, ts, te, 'm_IsExit:')
        dst_state_line = None
        for k in range(ts,te):
            if 'm_DstState:' in lines[k]:
                dst_state_line = lines[k].strip()
                break
        exit_details.append(f"tid={tid} HasExitTime={has_exit_time} IsExit={is_exit} dst={dst_state_line}")
    if transitions:
        print(f"  &{sfid} {name}: {len(transitions)} transitions")
        for d in exit_details[:2]:
            print(f"    {d}")
    else:
        print(f"  &{sfid} {name}: NO TRANSITIONS OUT ← stuck!")

# ── Check shooting trigger availability per UpperBody sub-machine ──
print(f"\n{'='*60}")
print("UPPERBODY SUB-MACHINES - Available shoot triggers")
print(f"{'='*60}")

UB_MACHINES = [
    (9200048, 'Handgun_UB'),
    (9200173, 'Infantry_UB'),
    (9200304, 'Heavy_UB'),
    (9200438, 'Knife_UB'),
    (9200569, 'Machinegun_UB'),
    (9200703, 'RocketLauncher_UB'),
    (9202010, 'Unarmed_UB'),
]
for mfid, mname in UB_MACHINES:
    start, end = find_block(mfid)
    shoot_caps = []
    for i in range(start, end):
        if 'm_ConditionEvent:' in lines[i]:
            ev = lines[i].split(':',1)[1].strip()
            if ev in ('Shoot','ShootBurst','ShootLoop','ShootBolt','ShootShotgun'):
                if ev not in shoot_caps:
                    shoot_caps.append(ev)
    print(f"  {mname}: {shoot_caps}")
