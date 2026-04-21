import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F, 'r', encoding='utf-8') as f:
    content = f.read()

DEATH_MACHINES = [
    (9200111, 'Handgun_Death'),
    (9200243, 'Infantry_Death'),
    (9200378, 'Heavy_Death'),
    (9200510, 'Knife_Death'),
    (9200645, 'Machinegun_Death'),
    (9200762, 'RocketLauncher_Death'),
    (9202220, 'Unarmed_Death'),
]

def get_state_name(fid):
    sb = re.search(rf'--- !u!1102 &{fid}\b(.*?)--- !u!', content, re.DOTALL)
    if not sb: return None
    nm = re.search(r'm_Name: (.+)', sb.group(1))
    return nm.group(1).strip() if nm else None

def get_state_transitions(fid):
    sb = re.search(rf'--- !u!1102 &{fid}\b(.*?)--- !u!', content, re.DOTALL)
    if not sb: return [], None, None
    block = sb.group(1)
    trans_section = re.search(r'm_Transitions:(.*?)m_StateMachineBehaviours:', block, re.DOTALL)
    trans_fids = re.findall(r'fileID: (\d+)', trans_section.group(1)) if trans_section else []
    default_m = re.search(r'm_DefaultState: \{fileID: (\d+)\}', block)
    return trans_fids, None, None

def get_sm_info(sm_fid):
    sm_block = re.search(rf'--- !u!\d+ &{sm_fid}\b(.*?)--- !u!', content, re.DOTALL)
    if not sm_block: return None, []
    block = sm_block.group(1)
    default = re.search(r'm_DefaultState: \{fileID: (\d+)\}', block)
    states_section = re.search(r'm_ChildStates:(.*?)m_ChildStateMachines:', block, re.DOTALL)
    fids = re.findall(r'm_State: \{fileID: (\d+)\}', states_section.group(1)) if states_section else []
    return (default.group(1) if default else None), fids

for sm_fid, name in DEATH_MACHINES:
    default_fid, state_fids = get_sm_info(sm_fid)
    print(f'\n{name} (SM:{sm_fid}):')
    print(f'  Default state: {default_fid} = {get_state_name(int(default_fid)) if default_fid else "?"}')
    for fid in state_fids:
        sname = get_state_name(int(fid))
        # Check transitions on this state
        sb = re.search(rf'--- !u!1102 &{fid}\b(.*?)--- !u!', content, re.DOTALL)
        trans_empty = 'm_Transitions: []' in sb.group(1) if sb else True
        print(f'  State {fid}: {sname} | transitions={"EMPTY" if trans_empty else "HAS"}')

# Check Death ROOT entry transitions
print('\n--- Death ROOT (9200850) entry transitions ---')
root_block = re.search(r'--- !u!\d+ &9200850\b(.*?)--- !u!', content, re.DOTALL)
if root_block:
    entry_section = re.search(r'm_EntryTransitions:(.*?)m_StateMachineTransitions:', root_block.group(1), re.DOTALL)
    entry_fids = re.findall(r'fileID: (\d+)', entry_section.group(1)) if entry_section else []
    for fid in entry_fids:
        tb = re.search(rf'--- !u!1109 &{fid}\b(.*?)--- !u!', content, re.DOTALL)
        if tb:
            dst_sm = re.search(r'm_DstStateMachine: \{fileID: (\d+)\}', tb.group(1))
            cond_ev = re.search(r'm_ConditionEvent: (\S+)', tb.group(1))
            cond_mode = re.search(r'm_ConditionMode: (\d+)', tb.group(1))
            thresh = re.search(r'm_EventTreshold: (\S+)', tb.group(1))
            print(f'  Entry &{fid}: SM={dst_sm.group(1) if dst_sm else "?"} | {cond_ev.group(1) if cond_ev else "none"} mode={cond_mode.group(1) if cond_mode else "?"} thresh={thresh.group(1) if thresh else "?"}')
