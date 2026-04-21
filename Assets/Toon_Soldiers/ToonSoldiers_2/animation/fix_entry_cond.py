"""
Fix Entry transitions: ConditionMode 6 (NotEqual) -> 5 (Equals)
Affects: UpperBody ROOT, Base Layer ROOT, Death ROOT entry transitions
!u!1109 AnimatorTransition (Entry only)
"""
import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F, 'r', encoding='utf-8') as f:
    content = f.read()

# Fix all AnimatorTransition (1109) blocks that have ConditionMode: 6 -> 5
# These are Entry transitions (type 1109, not 1101)
pattern = re.compile(r'(--- !u!1109 &\d+\n.*?(?=--- !u!|\Z))', re.DOTALL)

fixed = 0

def fix_entry(m):
    global fixed
    block = m.group(1)
    if 'm_ConditionMode: 6' not in block:
        return block
    # Count how many we fix per block
    count = block.count('m_ConditionMode: 6')
    new_block = block.replace('m_ConditionMode: 6', 'm_ConditionMode: 5')
    fixed += count
    return new_block

new_content = pattern.sub(fix_entry, content)

print(f"Fixed {fixed} ConditionMode 6->5 in Entry transitions")

# Verify: check all entry transitions now
def check_entry_modes(sm_fid, name):
    sm_block = re.search(rf'--- !u!\d+ &{sm_fid}\b(.*?)--- !u!', new_content, re.DOTALL)
    if not sm_block:
        print(f"  {name}: NOT FOUND")
        return
    entry_section = re.search(r'm_EntryTransitions:(.*?)m_StateMachineTransitions:', sm_block.group(1), re.DOTALL)
    if not entry_section:
        return
    entry_fids = re.findall(r'fileID: (\d+)', entry_section.group(1))
    for fid in entry_fids:
        tb = re.search(rf'--- !u!1109 &{fid}\b(.*?)--- !u!', new_content, re.DOTALL)
        if tb:
            dst_sm = re.search(r'm_DstStateMachine: \{fileID: (\d+)\}', tb.group(1))
            cond_mode = re.search(r'm_ConditionMode: (\d+)', tb.group(1))
            thresh = re.search(r'm_EventTreshold: (\S+)', tb.group(1))
            ev = re.search(r'm_ConditionEvent: (\S+)', tb.group(1))
            dsm = dst_sm.group(1) if dst_sm else '?'
            mode = cond_mode.group(1) if cond_mode else 'none'
            t = thresh.group(1) if thresh else '?'
            e = ev.group(1) if ev else 'none'
            mode_str = 'Equals' if mode=='5' else 'NotEqual' if mode=='6' else f'mode={mode}'
            print(f"    Entry &{fid}: {e} {mode_str} {t} -> SM:{dsm}")

print("\nVerification after fix:")
check_entry_modes(9200812, 'UpperBody ROOT')
check_entry_modes(9200774, 'Base Layer ROOT')
check_entry_modes(9200850, 'Death ROOT')

if fixed > 0:
    with open(F, 'w', encoding='utf-8') as f:
        f.write(new_content)
    print("\nFile saved.")
else:
    print("\nNothing to fix.")
