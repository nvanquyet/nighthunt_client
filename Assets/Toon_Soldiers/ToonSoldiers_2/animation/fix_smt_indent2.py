"""
Fix indentation of m_StateMachineTransitions — correct 2-space format.
"""
import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F, encoding='utf-8') as f:
    content = f.read()

UB_SMS   = [9200048, 9200174, 9200306, 9200441, 9200573, 9200708, 9202210]
BASE_SMS = [9200001, 9200127, 9200259, 9200394, 9200526, 9200661, 9202200]
DEATH_SMS= [9200111, 9200243, 9200378, 9200510, 9200645, 9200762, 9202220]

UB_TRANS    = list(range(9206000, 9206007))
BASE_TRANS  = list(range(9206010, 9206017))
DEATH_TRANS = list(range(9206020, 9206027))

ROOT_UB    = 9200812
ROOT_BASE  = 9200774
ROOT_DEATH = 9200850

def make_smt_yaml(sm_list, trans_fids):
    """
    Correct format (each line has its own spaces, no double-indent):
      m_StateMachineTransitions:
      - first: {fileID: X}
        second:
        - {fileID: Y}
    """
    lines = ['m_StateMachineTransitions:']
    all_sources = sm_list + [0]
    for sm_fid in all_sources:
        lines.append(f'  - first: {{fileID: {sm_fid}}}')
        lines.append(f'    second:')
        for tfid in trans_fids:
            lines.append(f'    - {{fileID: {tfid}}}')
    return '\n'.join(lines)  # simple join, no extra indent

def replace_smt(root_fid, sm_list, trans_fids, name):
    global content
    # Match the SMT block: starts with (possibly-indented) m_StateMachineTransitions
    # and ends before m_StateMachineBehaviours
    pattern = re.compile(
        r'(--- !u!\d+ &' + str(root_fid) + r'\b.*?)([ \t]+m_StateMachineTransitions:.*?)([ \t]+m_StateMachineBehaviours:)',
        re.DOTALL
    )
    m = pattern.search(content)
    if not m:
        print(f'ERROR: {name} root &{root_fid} not found')
        return

    # Get the leading indent of m_StateMachineTransitions
    indent = re.match(r'[ \t]*', m.group(2)).group(0)
    # Build new SMT with same base indent
    new_smt_lines = make_smt_yaml(sm_list, trans_fids).split('\n')
    # Add indent to each line
    new_smt = '\n'.join(indent + line if line else line for line in new_smt_lines)

    content = content[:m.start(2)] + new_smt + '\n' + content[m.start(3):]
    print(f'{name}: SMT replaced (indent={repr(indent)})')

replace_smt(ROOT_UB,    UB_SMS,    UB_TRANS,    'UpperBody')
replace_smt(ROOT_BASE,  BASE_SMS,  BASE_TRANS,  'Base Layer')
replace_smt(ROOT_DEATH, DEATH_SMS, DEATH_TRANS, 'Death')

# Verify: show first 300 chars of UB SMT
m = re.search(rf'--- !u!\d+ &{ROOT_UB}\b(.*?)(?=--- !u!)', content, re.DOTALL)
if m:
    smt = re.search(r'  m_StateMachineTransitions:(.*?)  m_StateMachineBehaviours:', m.group(1), re.DOTALL)
    if smt:
        print('\nUB SMT (first 350 chars):')
        print('  m_StateMachineTransitions:' + smt.group(1)[:320])

with open(F, 'w', encoding='utf-8') as f:
    f.write(content)
print('\nFile saved.')
