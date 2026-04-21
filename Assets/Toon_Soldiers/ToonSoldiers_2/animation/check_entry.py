import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F, 'r', encoding='utf-8') as f:
    content = f.read()

# Check UpperBody ROOT (9200812) entry transitions + anystate transitions
ub_root = 9200812
block_m = re.search(rf'--- !u!\d+ &{ub_root}\b(.*?)--- !u!', content, re.DOTALL)
if not block_m:
    print("UpperBody root NOT FOUND")
else:
    block = block_m.group(1)
    
    # Entry transitions
    entry_section = re.search(r'm_EntryTransitions:(.*?)m_StateMachineTransitions:', block, re.DOTALL)
    entry_fids = re.findall(r'fileID: (\d+)', entry_section.group(1)) if entry_section else []
    print(f"UpperBody ROOT entry transitions: {len(entry_fids)}")
    for fid in entry_fids:
        tb = re.search(rf'--- !u!1109 &{fid}\b(.*?)--- !u!', content, re.DOTALL)
        if tb:
            conds = re.findall(r'm_ConditionMode: (\d+)\s+.*?m_ConditionEvent: (\S+)\s+.*?m_EventTreshold: (\S+)', tb.group(1), re.DOTALL)
            conds2 = re.findall(r'm_ConditionEvent: (\S+).*?m_EventTreshold: (\S+).*?m_ConditionMode: (\d+)', tb.group(1), re.DOTALL)
            dst_sm = re.search(r'm_DstStateMachine: \{fileID: (\d+)\}', tb.group(1))
            dst_st = re.search(r'm_DstState: \{fileID: (\d+)\}', tb.group(1))
            dsm = dst_sm.group(1) if dst_sm else '0'
            dst = dst_st.group(1) if dst_st else '0'
            print(f"  Entry &{fid}: DstSM={dsm} DstState={dst}")
            # Print raw conditions section
            cond_raw = re.search(r'm_Conditions:(.*?)m_DstStateMachine:', tb.group(1), re.DOTALL)
            if cond_raw:
                print(f"    Conditions: {cond_raw.group(1).strip()[:200]}")
        else:
            print(f"  Entry &{fid}: NOT FOUND as 1109!")
            # Try as 1101
            tb2 = re.search(rf'--- !u!1101 &{fid}\b(.*?)--- !u!', content, re.DOTALL)
            if tb2:
                print(f"    Found as 1101 (StateTransition not EntryTransition!)")

    # AnyState transitions
    any_section = re.search(r'm_AnyStateTransitions:(.*?)m_EntryTransitions:', block, re.DOTALL)
    any_fids = re.findall(r'fileID: (\d+)', any_section.group(1)) if any_section else []
    print(f"\nUpperBody ROOT anystate transitions: {len(any_fids)}")
    for fid in any_fids[:5]:
        print(f"  AnyState &{fid}")

print()
# Also check Handgun_UB sub-machine entry
hg_ub = 9200048
block_m2 = re.search(rf'--- !u!\d+ &{hg_ub}\b(.*?)--- !u!', content, re.DOTALL)
if block_m2:
    block2 = block_m2.group(1)
    default = re.search(r'm_DefaultState: \{fileID: (\d+)\}', block2)
    entry_section2 = re.search(r'm_EntryTransitions:(.*?)m_StateMachineTransitions:', block2, re.DOTALL)
    entry_fids2 = re.findall(r'fileID: (\d+)', entry_section2.group(1)) if entry_section2 else []
    print(f"Handgun_UB default state: {default.group(1) if default else '?'}")
    print(f"Handgun_UB entry transitions: {len(entry_fids2)}")
