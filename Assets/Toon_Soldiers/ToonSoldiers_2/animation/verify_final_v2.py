import re
F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F,'r',encoding='utf-8') as f: content = f.read()
lines = content.splitlines(keepends=True)
print(f'Total lines: {len(lines)}')

def find_block(fid):
    pat = re.compile(rf'^--- !u!\d+ &{fid}\b', re.MULTILINE)
    m = pat.search(content)
    if not m: return -1, -1
    start_pos = content.rfind('\n', 0, m.start()) + 1
    next_m = re.search(r'\n--- !u!', content[m.start():])
    end_pos = m.start() + next_m.start() + 1 if next_m else len(content)
    block = content[m.start():end_pos]
    # Get line numbers
    start_line = content[:m.start()].count('\n')
    end_line = start_line + block.count('\n')
    return start_line, end_line

def get_anystate_count(machine_fid):
    pat = re.compile(rf'--- !u!\d+ &{machine_fid}\b.*?m_AnyStateTransitions:\n((?:  - \{{fileID: \d+\}}\n)*)', re.DOTALL)
    m = pat.search(content)
    if not m: return 0, []
    refs = re.findall(r'fileID: (\d+)', m.group(1))
    return len(refs), refs

print('\n[1] Trigger ConditionMode 9 remaining (all triggers):')
TRIGGERS = {'Shoot','ShootBurst','Reload','Draw','ThrowGrenade','Interact','TakeDamage','Attack','Die','Roll','Respawn'}
bad = 0
for m in re.finditer(r'- m_ConditionMode: 9\n\s+m_ConditionEvent: (\S+)', content):
    if m.group(1) in TRIGGERS:
        bad += 1
print(f'   Bad trigger conditions remaining: {bad} (want 0)')

print('\n[2] Base Layer AnyState transitions (want 1 each = WeaponExit):')
BASE = [(9200001,'Handgun_Base',0),(9200127,'Infantry_Base',1),(9200259,'Heavy_Base',2),
        (9200394,'Knife_Base',3),(9200526,'Machinegun_Base',4),(9200661,'RL_Base',5)]
for mfid, name, wtype in BASE:
    cnt, refs = get_anystate_count(mfid)
    print(f'   {name}: {cnt} AnyState refs', refs)

print('\n[3] UpperBody AnyState transitions:')
UB = [(9200048,'Handgun_UB',0),(9200174,'Infantry_UB',1),(9200306,'Heavy_UB',2),
      (9200441,'Knife_UB',3),(9200573,'Machinegun_UB',4),(9200708,'RL_UB',5)]
for mfid, name, wtype in UB:
    cnt, refs = get_anystate_count(mfid)
    print(f'   {name}: {cnt} AnyState refs')
    if cnt > 0:
        # Verify last one is Exit
        last_fid = int(refs[-1])
        last_block = re.search(rf'--- !u!1101 &{last_fid}\b.*?m_IsExit: (\d)', content, re.DOTALL)
        is_exit = last_block.group(1) if last_block else '?'
        print(f'     Last FID &{last_fid}: IsExit={is_exit} (want 1=exit)')

print('\n[4] Unarmed sub-machines in root machines:')
# Check root entry transitions include WT=6
for root_fid, rname in [(9200774,'Base_root'),(9200812,'UB_root'),(9200850,'Death_root')]:
    block_m = re.search(rf'--- !u!\d+ &{root_fid}\b.*?m_ChildStateMachines:(.*?)m_AnyStateTransitions:', content, re.DOTALL)
    if block_m:
        unarmed_ref = '9202200' in block_m.group(1) or '9202210' in block_m.group(1) or '9202220' in block_m.group(1)
        print(f'   {rname}: Unarmed child machine: {unarmed_ref}')
    # Check entry has WT=6 trans
    entry_m = re.search(rf'--- !u!\d+ &{root_fid}\b.*?m_EntryTransitions:(.*?)m_StateMachineTransitions:', content, re.DOTALL)
    if entry_m:
        entries = re.findall(r'fileID: (\d+)', entry_m.group(1))
        print(f'   {rname}: Entry transitions: {len(entries)}')

print('\n[5] Death AnyState - Die/Respawn triggers per weapon:')
# Check all death sub-machines have Die mode=1 conditions
DEATH_MACHINES = [(9200111,'HG_Death'),(9200243,'INF_Death'),(9200378,'HVY_Death'),
                  (9200510,'KNF_Death'),(9200645,'MG_Death'),(9200762,'RL_Death'),
                  (9202220,'Unarmed_Death')]
for mfid, name in DEATH_MACHINES:
    cnt, refs = get_anystate_count(mfid)
    print(f'   {name}: {cnt} AnyState refs')

print('\n[6] Sample: Verify Handgun_UB AnyState transitions target states correctly')
cnt, refs = get_anystate_count(9200048)
if refs:
    # Check first 3 transitions
    for ref in refs[:3]:
        tm = re.search(rf'--- !u!1101 &{ref}\b(.*?)m_IsExit:', content, re.DOTALL)
        if tm:
            dst = re.search(r'm_DstState: \{fileID: (\d+)\}', tm.group(1))
            cond = re.findall(r'm_ConditionEvent: (\S+)', tm.group(1))
            is_exit = re.search(r'm_IsExit: (\d)', tm.group(1))
            print(f'   &{ref}: conds={cond} dst={dst.group(1) if dst else "?"} IsExit={is_exit.group(1) if is_exit else "?"}')
