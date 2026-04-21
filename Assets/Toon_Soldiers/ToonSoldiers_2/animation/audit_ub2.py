import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F, encoding='utf-8') as f:
    content = f.read()

# Get all UB_Empty states and their full transition FID list
states = re.findall(r'--- !u!1102 &(\d+)\n(.*?)(?=--- !u!)', content, re.DOTALL)
ub_empty_states = [(fid, block) for fid, block in states if 'm_Name: UB_Empty' in block]
print(f'UB_Empty states: {len(ub_empty_states)}')

for fid, block in ub_empty_states:
    tm = re.search(r'm_Transitions:(.*?)m_StateMachineBehaviours:', block, re.DOTALL)
    if not tm:
        print(f'  State &{fid}: NO m_Transitions section')
        continue
    trans_fids = re.findall(r'fileID: (\d+)', tm.group(1))
    print(f'\n  State &{fid}: {len(trans_fids)} transitions')
    for tfid in trans_fids:
        tb = re.search(rf'--- !u!1101 &{tfid}\b\n(.*?)(?=--- !u!)', content, re.DOTALL)
        if not tb:
            print(f'    &{tfid}: NOT FOUND in file')
            continue
        tblock = tb.group(1)
        dst = re.search(r'm_DstState: \{fileID: (\d+)\}', tblock)
        # Get conditions block
        cond_block = re.search(r'm_Conditions:(.*?)(?=m_DstState|m_DstStateMachine|m_IsExit)', tblock, re.DOTALL)
        if cond_block:
            conds_text = cond_block.group(1).strip()
            if conds_text and conds_text != '[]':
                conds = re.findall(r'm_ConditionEvent: (\S+)\s+m_ConditionMode: (\d+)\s+m_EventTreshold: (\S+)', conds_text)
                if conds:
                    print(f'    &{tfid} -> {dst.group(1) if dst else "?"}: {conds}')
        # Show raw content for first 2 transitions
        if tfid in [trans_fids[0], trans_fids[1]] if len(trans_fids) > 1 else [trans_fids[0]]:
            print(f'    RAW &{tfid}:\n{tblock[:400]}')
            print('    ---')
