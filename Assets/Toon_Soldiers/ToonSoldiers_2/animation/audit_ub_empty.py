import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F, encoding='utf-8') as f:
    content = f.read()

# Find UB_Empty states
states = re.findall(r'--- !u!1102 &(\d+)\n(.*?)(?=--- !u!)', content, re.DOTALL)
ub_empty_states = [(fid, block) for fid, block in states if 'm_Name: UB_Empty' in block]
print(f'UB_Empty states found: {len(ub_empty_states)}')

# Unity ConditionMode: 1=If(trigger), 2=IfNot, 3=Greater, 4=Less, 6=Equals, 7=NotEqual
# Int params (InteractIndex, AttackIndex, WeaponType, DeathIndex) -> must use 3,4,6,7
# Trigger params -> must use 1
# Bool params -> must use 1,2
PARAM_TYPES = {
    'WeaponType': 'Int', 'DeathIndex': 'Int', 'InteractIndex': 'Int', 'AttackIndex': 'Int',
    'Shoot': 'Trigger', 'ShootBurst': 'Trigger', 'Reload': 'Trigger', 'Draw': 'Trigger',
    'ThrowGrenade': 'Trigger', 'Interact': 'Trigger', 'TakeDamage': 'Trigger',
    'Attack': 'Trigger', 'Die': 'Trigger', 'Roll': 'Trigger', 'Respawn': 'Trigger',
    'WeaponChanged': 'Trigger', 'WeaponChangedUB': 'Trigger', 'WeaponChangedDeath': 'Trigger',
    'IsCrouching': 'Bool', 'IsProne': 'Bool', 'IsGuard': 'Bool', 'IsSprinting': 'Bool',
    'IsGrounded': 'Bool', 'IsOnLadder': 'Bool', 'ShootLoop': 'Bool', 'ShootBolt': 'Bool',
    'ShootShotgun': 'Bool',
}
INT_MODES = {'3', '4', '6', '7'}
TRIGGER_MODES = {'1'}
BOOL_MODES = {'1', '2'}

bad_total = 0
for fid, block in ub_empty_states:
    tm = re.search(r'm_Transitions:(.*?)m_StateMachineBehaviours:', block, re.DOTALL)
    if not tm:
        continue
    trans_fids = re.findall(r'fileID: (\d+)', tm.group(1))
    for tfid in trans_fids:
        tb = re.search(rf'--- !u!1101 &{tfid}\n(.*?)(?=--- !u!)', content, re.DOTALL)
        if not tb:
            continue
        tblock = tb.group(1)
        conds = re.findall(r'm_ConditionEvent: (\S+)\s+m_ConditionMode: (\d+)\s+m_EventTreshold: (\S+)', tblock)
        dst = re.search(r'm_DstState: \{fileID: (\d+)\}', tblock)
        for ev, mode, thresh in conds:
            ptype = PARAM_TYPES.get(ev, 'Unknown')
            ok = True
            if ptype == 'Int' and mode not in INT_MODES:
                ok = False
            elif ptype == 'Trigger' and mode not in TRIGGER_MODES:
                ok = False
            elif ptype == 'Bool' and mode not in BOOL_MODES:
                ok = False
            if not ok:
                bad_total += 1
                print(f'  BAD: state={fid} trans=&{tfid} param={ev}({ptype}) mode={mode} thresh={thresh} dst={dst.group(1) if dst else "?"}')

if bad_total == 0:
    print('  No bad ConditionMode found in UB_Empty transitions')

# Also check AnyState transitions in UB sub-machines for bad modes
print('\n--- AnyState transitions with InteractIndex/AttackIndex ---')
anyst = re.findall(r'--- !u!1101 &(\d+)\n(.*?)(?=--- !u!)', content, re.DOTALL)
bad_any = 0
for fid, block in anyst:
    if 'm_IsExit: 0' not in block and 'm_IsExit: 1' not in block:
        continue
    conds = re.findall(r'm_ConditionEvent: (\S+)\s+m_ConditionMode: (\d+)\s+m_EventTreshold: (\S+)', block)
    for ev, mode, thresh in conds:
        ptype = PARAM_TYPES.get(ev, 'Unknown')
        if ptype == 'Int' and mode not in INT_MODES:
            bad_any += 1
            print(f'  BAD AnyState: &{fid} param={ev} mode={mode} thresh={thresh}')
if bad_any == 0:
    print('  None found in AnyState')

# Check AnyState transitions in UB sub-machines more broadly
print('\n--- All conditions in UB transitions range 9203000-9203125 ---')
for fid, block in anyst:
    if not (9203000 <= int(fid) <= 9203125):
        continue
    conds = re.findall(r'm_ConditionEvent: (\S+)\s+m_ConditionMode: (\d+)\s+m_EventTreshold: (\S+)', block)
    dst = re.search(r'm_DstState: \{fileID: (\d+)\}', block)
    print(f'  &{fid} -> {dst.group(1) if dst else "?"}: {conds}')
