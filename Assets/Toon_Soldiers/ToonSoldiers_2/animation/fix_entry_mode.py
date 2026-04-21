"""
Revert ConditionMode 5 -> 6 in Entry transitions (type 1109).
Unity AnimatorConditionMode: Equals=6, NotEqual=7 (mode 5 does NOT exist).
"""
import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F, 'r', encoding='utf-8') as f:
    content = f.read()

pattern = re.compile(r'(--- !u!1109 &\d+\n.*?(?=--- !u!|\Z))', re.DOTALL)

fixed = 0

def fix_entry(m):
    global fixed
    block = m.group(1)
    if 'm_ConditionMode: 5' not in block:
        return block
    new_block = block.replace('m_ConditionMode: 5', 'm_ConditionMode: 6')
    fixed += block.count('m_ConditionMode: 5')
    return new_block

new_content = pattern.sub(fix_entry, content)
print(f'Fixed {fixed} ConditionMode 5->6 in Entry transitions (1109)')

# Verify
def check(sm_fid, name):
    sm_block = re.search(rf'--- !u!\d+ &{sm_fid}\b(.*?)--- !u!', new_content, re.DOTALL)
    if not sm_block: return
    entry_section = re.search(r'm_EntryTransitions:(.*?)m_StateMachineTransitions:', sm_block.group(1), re.DOTALL)
    if not entry_section: return
    fids = re.findall(r'fileID: (\d+)', entry_section.group(1))
    for fid in fids:
        tb = re.search(rf'--- !u!1109 &{fid}\b(.*?)--- !u!', new_content, re.DOTALL)
        if not tb: continue
        dst_sm = re.search(r'm_DstStateMachine: \{fileID: (\d+)\}', tb.group(1))
        mode = re.search(r'm_ConditionMode: (\d+)', tb.group(1))
        thresh = re.search(r'm_EventTreshold: (\S+)', tb.group(1))
        ev = re.search(r'm_ConditionEvent: (\S+)', tb.group(1))
        mode_str = {6:'Equals', 7:'NotEqual', 3:'Greater', 4:'Less'}.get(int(mode.group(1)) if mode else -1, f'mode={mode.group(1) if mode else "?"}')
        print(f'  &{fid}: {ev.group(1) if ev else "?"} {mode_str} {thresh.group(1) if thresh else "?"} -> SM:{dst_sm.group(1) if dst_sm else "?"}')

print('\nBase Layer ROOT (9200774):')
check(9200774, 'Base')
print('\nUpperBody ROOT (9200812):')
check(9200812, 'UB')
print('\nDeath ROOT (9200850):')
check(9200850, 'Death')

with open(F, 'w', encoding='utf-8') as f:
    f.write(new_content)
print('\nFile saved.')
