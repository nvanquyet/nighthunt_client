"""
deep_audit.py - Kiểm tra toàn diện 5 vấn đề user yêu cầu
"""
import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F,'r',encoding='utf-8') as f: lines=f.readlines()
total = len(lines)
print(f"Total lines: {total}\n")

# Build index: FID -> (line_start, line_end)
fid_index = {}
for i, l in enumerate(lines):
    m = re.match(r'^--- !u!(\d+) &(\d+)', l)
    if m:
        if fid_index:
            prev = list(fid_index.keys())[-1]
            fid_index[prev] = (fid_index[prev][0], i)
        fid_index[int(m.group(2))] = (i, total)

def get_block(fid):
    if fid not in fid_index: return []
    s,e = fid_index[fid]
    return lines[s:e]

def get_field(block, key):
    for l in block:
        if key in l:
            return l.strip()
    return None

def get_yaml_list(block, list_key):
    """Return list of fileIDs from a YAML list like m_Transitions: [{fileID:X},...]"""
    result = []
    in_list = False
    for l in block:
        if list_key in l:
            if '[]' in l: return []
            in_list = True
            continue
        if in_list:
            m = re.search(r'fileID: (-?\d+)', l)
            if m: result.append(int(m.group(1)))
            elif l.strip() and not l.strip().startswith('-') and not l.strip().startswith('{'):
                break
            elif re.match(r'  \w', l): break
    return result

# ─────────────────────────────────────────────────────────────────────────────
# 1. LOCOMOTION CLIPS: Are different weapons using DIFFERENT motion clips?
# ─────────────────────────────────────────────────────────────────────────────
print("="*70)
print("1. LOCOMOTION MOTION CLIPS PER WEAPON")
print("="*70)

BASE_MACHINES = [
    (9200001,'Handgun_Base', 9200002),
    (9200126,'Infantry_Base', 9200127),
    (9200257,'Heavy_Base', 9200258),
    (9200391,'Knife_Base', 9200392),
    (9200522,'Machinegun_Base', 9200523),
    (9200656,'RocketLauncher_Base', 9200657),
    (9202000,'Unarmed_Base', 9202001),
]

loco_motions = {}
for machine_fid, machine_name, default_state_fid in BASE_MACHINES:
    b = get_block(default_state_fid)
    if not b:
        print(f"  {machine_name}: default state FID={default_state_fid} NOT FOUND")
        continue
    name_line = get_field(b, '  m_Name:')
    motion_line = get_field(b, '  m_Motion:')
    state_name = name_line.replace('m_Name:','').strip() if name_line else '?'
    if motion_line and 'fileID: 0' not in motion_line and 'guid' in motion_line:
        guid = re.search(r'guid: ([a-f0-9]+)', motion_line)
        guid_str = guid.group(1)[:12] + '...' if guid else '?'
        loco_motions[machine_name] = guid.group(1) if guid else None
        print(f"  {machine_name}: '{state_name}' -> {guid_str}")
    else:
        loco_motions[machine_name] = None
        print(f"  {machine_name}: '{state_name}' -> NO MOTION CLIP (empty)")

# Check if clips are all the same
guids = [v for v in loco_motions.values() if v]
unique_guids = set(guids)
print(f"\n  Unique locomotion GUIDs: {len(unique_guids)}")
if len(unique_guids) == 1:
    print("  WARNING: All weapons use SAME locomotion clip!")
elif len(unique_guids) > 1:
    print("  OK: Different weapons use different clips")
else:
    print("  WARNING: No clips assigned!")

# Check ALL states in Handgun_Base vs Infantry_Base to compare 
print("\n  === Comparing state counts and motion clips ===")
for machine_fid, machine_name, _ in BASE_MACHINES[:4]:
    b = get_block(machine_fid)
    state_fids = get_yaml_list(b, 'm_ChildStates:')
    if not state_fids:
        # parse manually
        state_fids = [int(m.group(1)) for l in b for m in [re.search(r'm_State: \{fileID: (\d+)\}', l)] if m]
    print(f"  {machine_name}: {len(state_fids)} states")
    for sfid in state_fids:
        sb = get_block(sfid)
        if not sb: continue
        sname = get_field(sb, '  m_Name:')
        sname = sname.replace('m_Name:','').strip() if sname else str(sfid)
        smot = get_field(sb, '  m_Motion:')
        has_clip = 'guid' in smot if smot else False
        print(f"    {sfid}: {sname} -> {'CLIP' if has_clip else 'empty'}")

# ─────────────────────────────────────────────────────────────────────────────
# 2. ENTRY TRANSITION ORDER — is the no-condition default BEFORE conditional?
# ─────────────────────────────────────────────────────────────────────────────
print("\n" + "="*70)
print("2. ENTRY TRANSITION ORDERING (Default must be LAST)")
print("="*70)

ROOT_MACHINES = [
    (9200768, 'Base Layer root'),
    (9200776, 'UpperBody root'),
    (9200784, 'Death root'),
]

for root_fid, root_name in ROOT_MACHINES:
    b = get_block(root_fid)
    entry_fids = []
    in_entry = False
    for l in b:
        if 'm_EntryTransitions:' in l:
            in_entry = True
            continue
        if in_entry:
            m = re.search(r'fileID: (-?\d+)', l)
            if m: entry_fids.append(int(m.group(1)))
            elif l.strip() and not l.strip().startswith('- {'):
                break
    
    print(f"\n  {root_name} ({root_fid}) Entry order:")
    default_pos = None
    for idx, efid in enumerate(entry_fids):
        eb = get_block(efid)
        conds = []
        in_cond = False
        for l in eb:
            if 'm_Conditions:' in l:
                if '[]' in l:
                    conds = []
                    break
                in_cond = True
            elif in_cond:
                if 'm_ConditionMode:' in l:
                    mode = re.search(r'm_ConditionMode: (\d+)', l)
                elif 'm_ConditionEvent:' in l:
                    evt = re.search(r'm_ConditionEvent: (\w+)', l)
                    conds.append(evt.group(1) if evt else '?')
        
        label = ', '.join(conds) if conds else '(no conditions = DEFAULT FALLBACK)'
        warning = ' <-- PROBLEM: default must be last!' if not conds and idx < len(entry_fids)-1 else ''
        print(f"    [{idx}] &{efid}: {label}{warning}")
        if not conds:
            default_pos = idx

# ─────────────────────────────────────────────────────────────────────────────
# 3. UPPERBODY ACTION STATES — do they have exit transitions?
# ─────────────────────────────────────────────────────────────────────────────
print("\n" + "="*70)
print("3. UPPERBODY ACTION STATES — exit transitions check")
print("="*70)

UB_MACHINES = [
    (9200048,'Handgun_UB'), (9200173,'Infantry_UB'), (9200304,'Heavy_UB'),
    (9200438,'Knife_UB'), (9200569,'MG_UB'), (9200703,'RL_UB'),
]

for machine_fid, machine_name in UB_MACHINES:
    b = get_block(machine_fid)
    state_fids = [int(m.group(1)) for l in b for m in [re.search(r'm_State: \{fileID: (\d+)\}', l)] if m]
    print(f"\n  {machine_name}:")
    for sfid in state_fids:
        sb = get_block(sfid)
        if not sb: continue
        sname_l = get_field(sb, '  m_Name:')
        sname = sname_l.replace('m_Name:','').strip() if sname_l else str(sfid)
        trans = [int(m.group(1)) for l in sb for m in [re.search(r'fileID: (-?\d+)', l)] if m and 'm_Transitions' not in l and 'm_State' not in l and 'm_StateMachine' not in l]
        trans_raw = get_field(sb, '  m_Transitions:')
        no_trans = '[]' in trans_raw if trans_raw else True
        
        # Check HasExitTime in its transitions
        if not no_trans:
            # Need to get transition blocks
            pass
        
        status = 'NO EXIT' if no_trans else f'{len(trans)} exits'
        # Special: check if transitions exist by looking at m_Transitions line
        for l in sb:
            if '  m_Transitions:' in l:
                if '[]' in l:
                    status = 'STUCK (no transitions out)'
                else:
                    # count refs
                    cnt = 0
                    idx2 = sb.index(l) + 1
                    while idx2 < len(sb):
                        if re.search(r'fileID:', sb[idx2]):
                            cnt += 1
                            idx2 += 1
                        else:
                            break
                    status = f'OK ({cnt} transitions)'
                break
        
        is_action = sname not in ['UB_Empty']
        marker = '  *** ACTION STATE ***' if is_action else ''
        print(f"    {sfid}: {sname} -> {status}{marker}")

# ─────────────────────────────────────────────────────────────────────────────
# 4. DEATH STATES — loop setting + motion check
# ─────────────────────────────────────────────────────────────────────────────
print("\n" + "="*70)
print("4. DEATH STATES — loop setting check")
print("="*70)

DEATH_MACHINES = [
    (9200111,'Handgun_Death'),
    (9200242,'Infantry_Death'),
]

for machine_fid, machine_name in DEATH_MACHINES:
    b = get_block(machine_fid)
    state_fids = [int(m.group(1)) for l in b for m in [re.search(r'm_State: \{fileID: (\d+)\}', l)] if m]
    print(f"\n  {machine_name}:")
    for sfid in state_fids:
        sb = get_block(sfid)
        if not sb: continue
        sname_l = get_field(sb, '  m_Name:')
        sname = sname_l.replace('m_Name:','').strip() if sname_l else str(sfid)
        smot = get_field(sb, '  m_Motion:')
        guid = re.search(r'guid: ([a-f0-9]{8})', smot) if smot else None
        guid_str = guid.group(1) + '...' if guid else 'NO CLIP'
        # Check transitions
        no_trans = any('m_Transitions: []' in l for l in sb)
        print(f"    {sfid}: {sname} -> {guid_str} | exits: {'NONE (holds at last frame)' if no_trans else 'HAS EXITS'}")

# Now check if Handgun_Death and Infantry_Death use SAME death clips
print("\n  === Comparing Death clips across weapons ===")
death_guids_per_weapon = {}
DEATH_ALL = [
    (9200111,'Handgun'), (9200242,'Infantry'), (9200376,'Heavy'),
    (9200507,'Knife'), (9200641,'Machinegun'), (9200757,'RocketLauncher'),
]
for machine_fid, weapon_name in DEATH_ALL:
    b = get_block(machine_fid)
    state_fids = [int(m.group(1)) for l in b for m in [re.search(r'm_State: \{fileID: (\d+)\}', l)] if m]
    clips = []
    for sfid in state_fids:
        sb = get_block(sfid)
        if not sb: continue
        smot = get_field(sb, '  m_Motion:')
        if smot and 'guid' in smot:
            g = re.search(r'guid: ([a-f0-9]+)', smot)
            if g: clips.append(g.group(1)[:8])
    death_guids_per_weapon[weapon_name] = clips

handgun_clips = death_guids_per_weapon.get('Handgun',[])
for weapon, clips in death_guids_per_weapon.items():
    same = clips == handgun_clips
    print(f"  {weapon}: {clips} {'== SAME as Handgun' if same and weapon != 'Handgun' else ''}")

# ─────────────────────────────────────────────────────────────────────────────
# 5. SHOOTING LOGIC PER WEAPON
# ─────────────────────────────────────────────────────────────────────────────
print("\n" + "="*70)
print("5. SHOOTING TRANSITIONS PER WEAPON (AnyState)")
print("="*70)

UB_ALL = [
    (9200048,'Handgun'), (9200173,'Infantry'), (9200304,'Heavy'),
    (9200438,'Knife'), (9200569,'Machinegun'), (9200703,'RocketLauncher'),
    (9202010,'Unarmed'),
]
for machine_fid, weapon_name in UB_ALL:
    b = get_block(machine_fid)
    anystate_fids = []
    in_any = False
    for l in b:
        if 'm_AnyStateTransitions:' in l:
            if '[]' in l: break
            in_any = True
            continue
        if in_any:
            m = re.search(r'fileID: (-?\d+)', l)
            if m: anystate_fids.append(int(m.group(1)))
            elif l.strip() and not l.strip().startswith('- {'):
                break
    
    shoot_actions = []
    for tfid in anystate_fids:
        tb = get_block(tfid)
        if not tb: continue
        dst_fid = None
        cond_events = []
        for l in tb:
            m = re.search(r'm_DstState: \{fileID: (\d+)\}', l)
            if m and int(m.group(1)) != 0: dst_fid = int(m.group(1))
            me = re.search(r'm_ConditionEvent: (\w+)', l)
            if me: cond_events.append(me.group(1))
        if dst_fid and 0 not in [tfid]:
            dst_b = get_block(dst_fid)
            dst_name_l = get_field(dst_b, '  m_Name:') if dst_b else None
            dst_name = dst_name_l.replace('m_Name:','').strip() if dst_name_l else str(dst_fid)
            shoot_actions.append(f"{' + '.join(cond_events)} -> {dst_name}")
    
    print(f"\n  {weapon_name}:")
    for a in shoot_actions:
        print(f"    {a}")

print("\n" + "="*70)
print("DONE")
print("="*70)
