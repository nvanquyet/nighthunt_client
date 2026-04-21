"""
Comprehensive animator fix:
1. Add Holster trigger parameter
2. Add Holster states (Draw reversed, speed=-1) to all 6 armed weapon UB SMs
3. Add UB_Empty -> Holster transitions per weapon per stance
4. Fix Knife UB_Empty: remove Shoot/Reload transitions
5. Add Knife UB_Empty -> Attack_Prone transition
6. Add Unarmed attack states + transitions to Unarmed UB SM
7. Improve transition smoothness (dur UB_Empty->action: 0.05->0.08)
"""

import re

PATH = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
content = open(PATH, encoding='utf-8').read()

# ─── Weapon data ───────────────────────────────────────────
WEAPONS = [
    # (weapon_idx, name, sm_fid, ub_empty_fid, draw_stand, draw_crouch, draw_prone,
    #  hol_stand, hol_crouch, hol_prone, hol_exit_s, hol_exit_c, hol_exit_p,
    #  hol_tr_s, hol_tr_c, hol_tr_p)
    (1, 'Handgun',     '9200048', '9200049', '9200050', '9200066', '9200078',
        '9207000', '9207001', '9207002',  '9207100', '9207101', '9207102',
        '9207200', '9207201', '9207202'),
    (2, 'Infantry',    '9200174', '9200175', '9200176', '9200196', '9200208',
        '9207003', '9207004', '9207005',  '9207103', '9207104', '9207105',
        '9207203', '9207204', '9207205'),
    (3, 'Heavy',       '9200306', '9200307', '9200308', '9200326', '9200340',
        '9207006', '9207007', '9207008',  '9207106', '9207107', '9207108',
        '9207206', '9207207', '9207208'),
    (4, 'Knife',       '9200441', '9200442', '9200443', '9200461', '9200475',
        '9207009', '9207010', '9207011',  '9207109', '9207110', '9207111',
        '9207209', '9207210', '9207211'),
    (5, 'Machinegun',  '9200573', '9200574', '9200575', '9200593', '9200607',
        '9207012', '9207013', '9207014',  '9207112', '9207113', '9207114',
        '9207212', '9207213', '9207214'),
    (6, 'RL',          '9200708', '9200709', '9200710', '9200724', '9200734',
        '9207015', '9207016', '9207017',  '9207115', '9207116', '9207117',
        '9207215', '9207216', '9207217'),
]

KNIFE_SM = '9200441'
KNIFE_UB_EMPTY = '9200442'
KNIFE_ATTACK_PRONE_FID = '9200485'
KNIFE_ATTACK_PRONE_UB_TRANS = '9207300'

UNARMED_SM = '9202210'
UNARMED_UB_EMPTY = '9202211'
# Unarmed attack states - use Knife attack clips (same motions)
KNIFE_ATTACK_CLIPS = {
    'Attack_A_Stand':  ('9200457', '6b1b3f222fdcf014f8710050a779544e'),
    'Attack_B_Stand':  ('9200459', '14a6cfa2b968de049a6c79eb177c72d0'),
    'Attack_A_Crouch': ('9200471', '52d5e57f1eb5ec14fa664a04cacc2108'),
    'Attack_B_Crouch': ('9200473', '25790d786874b0d4792193300a38ab87'),
    'Attack_Prone':    ('9200485', 'e5e780270acfb3b4ba7a145d999b3ee0'),
}
UNARMED_ATTACK_STATES = {
    'Attack_A_Stand':  '9207400',
    'Attack_B_Stand':  '9207401',
    'Attack_A_Crouch': '9207402',
    'Attack_B_Crouch': '9207403',
    'Attack_Prone':    '9207404',
}
UNARMED_ATTACK_EXIT_TRANS = {
    'Attack_A_Stand':  '9207410',
    'Attack_B_Stand':  '9207411',
    'Attack_A_Crouch': '9207412',
    'Attack_B_Crouch': '9207413',
    'Attack_Prone':    '9207414',
}
UNARMED_UB_TRANS = {
    'Attack_A_Stand':  '9207420',
    'Attack_B_Stand':  '9207421',
    'Attack_A_Crouch': '9207422',
    'Attack_B_Crouch': '9207423',
    'Attack_Prone':    '9207424',
}

# ─── Extract Draw motion GUIDs from existing states ────────
def get_motion_guid(state_fid):
    pattern = r'--- !u!1102 &' + state_fid + r'\n.*?m_Motion: \{fileID: (\d+), guid: ([0-9a-f]+)'
    m = re.search(pattern, content, re.DOTALL)
    if m:
        return m.group(1), m.group(2)
    return '7400000', ''

# Get Knife attack clip GUIDs from existing states
def get_knife_clip_guid(state_fid):
    fid, guid = get_motion_guid(state_fid)
    return fid, guid

# ─── YAML generators ───────────────────────────────────────

def make_holster_state(state_fid, exit_trans_fid, motion_fid, motion_guid, stance, x_pos, y_pos):
    return f'''--- !u!1102 &{state_fid}
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: Holster_{stance}
  m_Speed: -1
  m_CycleOffset: 0
  m_Transitions:
  - {{fileID: {exit_trans_fid}}}
  m_StateMachineBehaviours: []
  m_Position: {{x: {x_pos}, y: {y_pos}, z: 0}}
  m_IKOnFeet: 0
  m_WriteDefaultValues: 1
  m_Mirror: 0
  m_SpeedParameterActive: 0
  m_MirrorParameterActive: 0
  m_CycleOffsetParameterActive: 0
  m_TimeParameterActive: 0
  m_Motion: {{fileID: {motion_fid}, guid: {motion_guid}, type: 3}}
  m_Tag: 
  m_SpeedParameter: 
  m_MirrorParameter: 
  m_CycleOffsetParameter: 
  m_TimeParameter: 
'''

def make_holster_exit_trans(exit_fid):
    return f'''--- !u!1101 &{exit_fid}
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: 
  m_Conditions: []
  m_DstStateMachine: {{fileID: 0}}
  m_DstState: {{fileID: 0}}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 1
  serializedVersion: 3
  m_TransitionDuration: 0.1
  m_TransitionOffset: 0
  m_ExitTime: 0.05
  m_HasExitTime: 1
  m_HasFixedDuration: 0
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 0
'''

def make_ub_to_holster_trans(trans_fid, holster_state_fid, stance):
    if stance == 'Stand':
        conditions = '''  m_Conditions:
  - m_ConditionMode: 1
    m_ConditionEvent: Holster
    m_EventTreshold: 0
  - m_ConditionMode: 2
    m_ConditionEvent: IsCrouching
    m_EventTreshold: 0
  - m_ConditionMode: 2
    m_ConditionEvent: IsProne
    m_EventTreshold: 0'''
    elif stance == 'Crouch':
        conditions = '''  m_Conditions:
  - m_ConditionMode: 1
    m_ConditionEvent: Holster
    m_EventTreshold: 0
  - m_ConditionMode: 1
    m_ConditionEvent: IsCrouching
    m_EventTreshold: 0'''
    else:  # Prone
        conditions = '''  m_Conditions:
  - m_ConditionMode: 1
    m_ConditionEvent: Holster
    m_EventTreshold: 0
  - m_ConditionMode: 1
    m_ConditionEvent: IsProne
    m_EventTreshold: 0'''

    return f'''--- !u!1101 &{trans_fid}
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: 
{conditions}
  m_DstStateMachine: {{fileID: 0}}
  m_DstState: {{fileID: {holster_state_fid}}}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.05
  m_TransitionOffset: 1
  m_ExitTime: 0.9
  m_HasExitTime: 0
  m_HasFixedDuration: 0
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 0
'''

def make_attack_state(state_fid, exit_fid, name, motion_fid, motion_guid, x_pos, y_pos):
    return f'''--- !u!1102 &{state_fid}
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Speed: 1
  m_CycleOffset: 0
  m_Transitions:
  - {{fileID: {exit_fid}}}
  m_StateMachineBehaviours: []
  m_Position: {{x: {x_pos}, y: {y_pos}, z: 0}}
  m_IKOnFeet: 0
  m_WriteDefaultValues: 1
  m_Mirror: 0
  m_SpeedParameterActive: 0
  m_MirrorParameterActive: 0
  m_CycleOffsetParameterActive: 0
  m_TimeParameterActive: 0
  m_Motion: {{fileID: {motion_fid}, guid: {motion_guid}, type: 3}}
  m_Tag: 
  m_SpeedParameter: 
  m_MirrorParameter: 
  m_CycleOffsetParameter: 
  m_TimeParameter: 
'''

def make_attack_exit_trans(exit_fid, duration='0.08', exit_time='0.95'):
    return f'''--- !u!1101 &{exit_fid}
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: 
  m_Conditions: []
  m_DstStateMachine: {{fileID: 0}}
  m_DstState: {{fileID: 0}}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 1
  serializedVersion: 3
  m_TransitionDuration: {duration}
  m_TransitionOffset: 0
  m_ExitTime: {exit_time}
  m_HasExitTime: 1
  m_HasFixedDuration: 0
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 0
'''

def make_ub_to_attack_trans(trans_fid, dst_state_fid, attack_idx, stance):
    conds = f'''  m_Conditions:
  - m_ConditionMode: 1
    m_ConditionEvent: Attack
    m_EventTreshold: 0
  - m_ConditionMode: 6
    m_ConditionEvent: AttackIndex
    m_EventTreshold: {attack_idx}'''
    if stance == 'Stand':
        conds += '''
  - m_ConditionMode: 2
    m_ConditionEvent: IsCrouching
    m_EventTreshold: 0
  - m_ConditionMode: 2
    m_ConditionEvent: IsProne
    m_EventTreshold: 0'''
    elif stance == 'Crouch':
        conds += '''
  - m_ConditionMode: 1
    m_ConditionEvent: IsCrouching
    m_EventTreshold: 0'''
    else:  # Prone
        conds += '''
  - m_ConditionMode: 1
    m_ConditionEvent: IsProne
    m_EventTreshold: 0'''

    return f'''--- !u!1101 &{trans_fid}
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: 
{conds}
  m_DstStateMachine: {{fileID: 0}}
  m_DstState: {{fileID: {dst_state_fid}}}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.08
  m_TransitionOffset: 0
  m_ExitTime: 0.9
  m_HasExitTime: 0
  m_HasFixedDuration: 0
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 0
'''

def make_knife_attack_prone_trans(trans_fid, dst_state_fid):
    return f'''--- !u!1101 &{trans_fid}
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: 
  m_Conditions:
  - m_ConditionMode: 1
    m_ConditionEvent: Attack
    m_EventTreshold: 0
  - m_ConditionMode: 1
    m_ConditionEvent: IsProne
    m_EventTreshold: 0
  m_DstStateMachine: {{fileID: 0}}
  m_DstState: {{fileID: {dst_state_fid}}}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.08
  m_TransitionOffset: 0
  m_ExitTime: 0.9
  m_HasExitTime: 0
  m_HasFixedDuration: 0
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 0
'''

# ─── Collect new YAML blocks ───────────────────────────────
new_blocks = []

# Data for SM and UB_Empty modifications
sm_new_states = {}    # sm_fid -> list of new state entries (YAML snippet for m_ChildStates)
ub_new_trans = {}     # ub_empty_fid -> list of new transition FIDs

for W in WEAPONS:
    (widx, wname, sm_fid, ub_empty_fid,
     draw_s, draw_c, draw_p,
     hol_s, hol_c, hol_p,
     hol_exit_s, hol_exit_c, hol_exit_p,
     hol_tr_s, hol_tr_c, hol_tr_p) = W

    # Get Draw motion GUIDs
    mfid_s, mguid_s = get_motion_guid(draw_s)
    mfid_c, mguid_c = get_motion_guid(draw_c)
    mfid_p, mguid_p = get_motion_guid(draw_p)

    if not mguid_s:
        print(f'WARNING: No motion GUID for {wname} Draw_Stand ({draw_s})')
        continue

    # Holster states
    new_blocks.append(make_holster_state(hol_s, hol_exit_s, mfid_s, mguid_s, 'Stand', 400, -100))
    new_blocks.append(make_holster_state(hol_c, hol_exit_c, mfid_c, mguid_c, 'Crouch', 400, 100))
    new_blocks.append(make_holster_state(hol_p, hol_exit_p, mfid_p, mguid_p, 'Prone', 400, 300))

    # Holster exit transitions
    new_blocks.append(make_holster_exit_trans(hol_exit_s))
    new_blocks.append(make_holster_exit_trans(hol_exit_c))
    new_blocks.append(make_holster_exit_trans(hol_exit_p))

    # UB_Empty → Holster transitions
    new_blocks.append(make_ub_to_holster_trans(hol_tr_s, hol_s, 'Stand'))
    new_blocks.append(make_ub_to_holster_trans(hol_tr_c, hol_c, 'Crouch'))
    new_blocks.append(make_ub_to_holster_trans(hol_tr_p, hol_p, 'Prone'))

    # Track what to add to SM m_ChildStates
    sm_new_states.setdefault(sm_fid, [])
    for fid, stance, yx in [(hol_s, 'Stand', (-100, 400)), (hol_c, 'Crouch', (100, 400)), (hol_p, 'Prone', (300, 400))]:
        sm_new_states[sm_fid].append(f'  - serializedVersion: 1\n    m_State: {{fileID: {fid}}}\n    m_Position: {{x: {yx[1]}, y: {yx[0]}, z: 0}}\n')

    # Track what to add to UB_Empty m_Transitions
    ub_new_trans.setdefault(ub_empty_fid, [])
    ub_new_trans[ub_empty_fid].extend([hol_tr_s, hol_tr_c, hol_tr_p])

# ─── Knife Attack_Prone transition ─────────────────────────
new_blocks.append(make_knife_attack_prone_trans(KNIFE_ATTACK_PRONE_UB_TRANS, KNIFE_ATTACK_PRONE_FID))
ub_new_trans.setdefault(KNIFE_UB_EMPTY, [])
ub_new_trans[KNIFE_UB_EMPTY].append(KNIFE_ATTACK_PRONE_UB_TRANS)

# ─── Unarmed attack states ─────────────────────────────────
attack_positions = {
    'Attack_A_Stand':  (600, -200),
    'Attack_B_Stand':  (600, -100),
    'Attack_A_Crouch': (600, 0),
    'Attack_B_Crouch': (600, 100),
    'Attack_Prone':    (600, 200),
}
for aname, state_fid in UNARMED_ATTACK_STATES.items():
    exit_fid = UNARMED_ATTACK_EXIT_TRANS[aname]
    src_fid, motion_guid = get_knife_clip_guid(KNIFE_ATTACK_CLIPS[aname][0])
    if not motion_guid:
        motion_guid = KNIFE_ATTACK_CLIPS[aname][1]
    ypos, xpos = attack_positions[aname]
    new_blocks.append(make_attack_state(state_fid, exit_fid, aname, '7400000', motion_guid, xpos, ypos))
    new_blocks.append(make_attack_exit_trans(exit_fid))

sm_new_states.setdefault(UNARMED_SM, [])
for aname, state_fid in UNARMED_ATTACK_STATES.items():
    ypos, xpos = attack_positions[aname]
    sm_new_states[UNARMED_SM].append(f'  - serializedVersion: 1\n    m_State: {{fileID: {state_fid}}}\n    m_Position: {{x: {xpos}, y: {ypos}, z: 0}}\n')

ub_new_trans.setdefault(UNARMED_UB_EMPTY, [])
# Transitions: A Stand, B Stand, A Crouch, B Crouch, Prone
new_blocks.append(make_ub_to_attack_trans(UNARMED_UB_TRANS['Attack_A_Stand'], UNARMED_ATTACK_STATES['Attack_A_Stand'], 0, 'Stand'))
new_blocks.append(make_ub_to_attack_trans(UNARMED_UB_TRANS['Attack_B_Stand'], UNARMED_ATTACK_STATES['Attack_B_Stand'], 1, 'Stand'))
new_blocks.append(make_ub_to_attack_trans(UNARMED_UB_TRANS['Attack_A_Crouch'], UNARMED_ATTACK_STATES['Attack_A_Crouch'], 0, 'Crouch'))
new_blocks.append(make_ub_to_attack_trans(UNARMED_UB_TRANS['Attack_B_Crouch'], UNARMED_ATTACK_STATES['Attack_B_Crouch'], 1, 'Crouch'))
new_blocks.append(make_ub_to_attack_trans(UNARMED_UB_TRANS['Attack_Prone'], UNARMED_ATTACK_STATES['Attack_Prone'], 0, 'Prone'))
ub_new_trans[UNARMED_UB_EMPTY].extend([
    UNARMED_UB_TRANS['Attack_A_Stand'],
    UNARMED_UB_TRANS['Attack_B_Stand'],
    UNARMED_UB_TRANS['Attack_A_Crouch'],
    UNARMED_UB_TRANS['Attack_B_Crouch'],
    UNARMED_UB_TRANS['Attack_Prone'],
])

# ─── Apply modifications ───────────────────────────────────
print(f"Generated {len(new_blocks)} new YAML blocks")

# 1. Add Holster parameter after Draw parameter
HOLSTER_PARAM = '''  - m_Name: Holster
    m_Type: 9
    m_DefaultFloat: 0
    m_DefaultInt: 0
    m_DefaultBool: 0
    m_Controller: {fileID: 9100000}
'''
# Find Draw param block (unique enough context)
old_draw_param_end = '  - m_Name: Draw\n    m_Type: 9\n    m_DefaultFloat: 0\n    m_DefaultInt: 0\n    m_DefaultBool: 0\n    m_Controller: {fileID: 9100000}\n'
new_draw_param_end = old_draw_param_end + HOLSTER_PARAM

if '  - m_Name: Holster\n' in content:
    print('Holster parameter already exists - skipping')
elif old_draw_param_end in content:
    content = content.replace(old_draw_param_end, new_draw_param_end, 1)
    print('Added Holster parameter')
else:
    print('WARNING: Could not find Draw parameter block to insert after')

# 2. Add states to SM m_ChildStates (insert before m_ChildStateMachines: [])
for sm_fid, state_entries in sm_new_states.items():
    # Find SM block and insert states before m_ChildStateMachines
    sm_marker = f'--- !u!1107 &{sm_fid}\n'
    sm_idx = content.find(sm_marker)
    if sm_idx == -1:
        print(f'WARNING: SM {sm_fid} not found')
        continue
    # Find m_ChildStateMachines: [] within this SM block
    child_sm_pattern = '  m_ChildStateMachines: []\n'
    # Find from sm_idx
    csm_idx = content.find(child_sm_pattern, sm_idx)
    if csm_idx == -1:
        print(f'WARNING: m_ChildStateMachines not found in SM {sm_fid}')
        continue
    insert_text = ''.join(state_entries)
    content = content[:csm_idx] + insert_text + content[csm_idx:]
    print(f'Added {len(state_entries)} states to SM {sm_fid}')

# 3. Add transitions to UB_Empty m_Transitions (before m_StateMachineBehaviours: [])
for ub_fid, trans_fids in ub_new_trans.items():
    ub_marker = f'--- !u!1102 &{ub_fid}\n'
    ub_idx = content.find(ub_marker)
    if ub_idx == -1:
        print(f'WARNING: UB_Empty {ub_fid} not found')
        continue
    # Find m_StateMachineBehaviours: [] within this block (it's the FIRST occurrence after ub_idx)
    smb_pattern = '  m_StateMachineBehaviours: []\n'
    smb_idx = content.find(smb_pattern, ub_idx)
    if smb_idx == -1:
        print(f'WARNING: m_StateMachineBehaviours not found in UB_Empty {ub_fid}')
        continue
    insert_text = ''.join(f'  - {{fileID: {fid}}}\n' for fid in trans_fids)
    content = content[:smb_idx] + insert_text + content[smb_idx:]
    print(f'Added {len(trans_fids)} transitions to UB_Empty {ub_fid}')

# 4. Fix Knife UB_Empty: remove Shoot_Stand/Crouch/Prone and Reload_Stand/Crouch/Prone from m_Transitions
# From audit: these are FIDs 9203066,9203067,9203073,9203074,9203078,9203079
KNIFE_REMOVE_TRANS = ['9203066', '9203067', '9203073', '9203074', '9203078', '9203079']
for fid in KNIFE_REMOVE_TRANS:
    entry = f'  - {{fileID: {fid}}}\n'
    if entry in content:
        content = content.replace(entry, '', 1)
        print(f'Removed Knife transition {fid} from UB_Empty')
    else:
        print(f'WARNING: Knife transition {fid} not found in content')

# 5. Smooth UB_Empty -> action transitions: dur 0.05 -> 0.08
# These are in specific 1101 blocks. Look for the pattern:
# m_TransitionDuration: 0.05 in UB_Empty outgoing transitions
# But only for UB_Empty->action (not from action->Exit which have exit conditions)
# Strategy: update transitions that have m_HasExitTime: 0 and m_TransitionDuration: 0.05 to 0.075
# This affects all UB_Empty outgoing transitions across all weapons
count_smooth = content.count('  m_TransitionDuration: 0.05\n')
print(f'Found {count_smooth} occurrences of TransitionDuration: 0.05')
# Only update in 1101 blocks that go from UB_Empty (hasExitTime=0, no conditions that include exitTime)
# Simple approach: ALL transitions with dur=0.05 that also have m_HasExitTime: 0
# We'll do this more carefully by looking at each 1101 block
# For now, let's increase ALL transitions with dur=0.05 to 0.075 that have m_HasExitTime: 0
# Pattern: these two lines appear together in UB->action transitions:
ub_action_dur_pattern = '  m_HasExitTime: 0\n  m_HasFixedDuration: 0\n'
# Actually dur appears before HasExitTime in our YAML format, so let's be more targeted
# The exact pattern from existing UB_Empty transitions:
old_ub_dur = '  m_TransitionDuration: 0.05\n  m_TransitionOffset: 0\n  m_ExitTime: 0.9\n  m_HasExitTime: 0\n'
new_ub_dur = '  m_TransitionDuration: 0.075\n  m_TransitionOffset: 0\n  m_ExitTime: 0.9\n  m_HasExitTime: 0\n'
cnt = content.count(old_ub_dur)
content = content.replace(old_ub_dur, new_ub_dur)
print(f'Smoothed {cnt} UB->action transition durations (0.05->0.075)')

# ─── Append new blocks ─────────────────────────────────────
if not content.endswith('\n'):
    content += '\n'
content += ''.join(new_blocks)

open(PATH, 'w', encoding='utf-8', newline='\n').write(content)
print()
print('=== DONE ===')
print(f'File written.')
