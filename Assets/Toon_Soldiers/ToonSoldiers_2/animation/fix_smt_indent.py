"""
Fix indentation of m_StateMachineTransitions in all 3 roots.
Correct: 2-space indent for items.
"""
import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F, encoding='utf-8') as f:
    content = f.read()

# UB SM FIDs
UB_SMS  = [9200048, 9200174, 9200306, 9200441, 9200573, 9200708, 9202210]
BASE_SMS= [9200001, 9200127, 9200259, 9200394, 9200526, 9200661, 9202200]
DEATH_SMS=[9200111, 9200243, 9200378, 9200510, 9200645, 9200762, 9202220]

UB_TRANS    = list(range(9206000, 9206007))
BASE_TRANS  = list(range(9206010, 9206017))
DEATH_TRANS = list(range(9206020, 9206027))

ROOT_UB    = 9200812
ROOT_BASE  = 9200774
ROOT_DEATH = 9200850

def make_smt_yaml(sm_list, trans_fids):
    """Generate correct 2-space-indented m_StateMachineTransitions YAML."""
    lines = ['m_StateMachineTransitions:']
    all_sources = sm_list + [0]  # include fileID:0 as fallback
    for sm_fid in all_sources:
        lines.append(f'  - first: {{fileID: {sm_fid}}}')
        lines.append(f'    second:')
        for tfid in trans_fids:
            lines.append(f'    - {{fileID: {tfid}}}')
    return '\n  '.join(lines)  # each line gets 2-space prefix from the outer indent

def replace_smt(root_fid, sm_list, trans_fids, name):
    global content
    # Find the entire StateMachineTransitions block
    pattern = re.compile(
        r'(--- !u!\d+ &' + str(root_fid) + r'\b.*?)'
        r'(m_StateMachineTransitions:.*?)'
        r'(m_StateMachineBehaviours:)',
        re.DOTALL
    )
    m = pattern.search(content)
    if not m:
        print(f'ERROR: {name} root &{root_fid} not found')
        return

    new_smt = make_smt_yaml(sm_list, trans_fids)
    content = content[:m.start(2)] + new_smt + '\n  ' + content[m.start(3):]
    print(f'{name} root: replaced m_StateMachineTransitions')

replace_smt(ROOT_UB,    UB_SMS,    UB_TRANS,    'UpperBody')
replace_smt(ROOT_BASE,  BASE_SMS,  BASE_TRANS,  'Base Layer')
replace_smt(ROOT_DEATH, DEATH_SMS, DEATH_TRANS, 'Death')

# Verify indentation
for root_fid, name in [(ROOT_UB, 'UpperBody'), (ROOT_BASE, 'Base'), (ROOT_DEATH, 'Death')]:
    m = re.search(rf'--- !u!\d+ &{root_fid}\b(.*?)(?=--- !u!)', content, re.DOTALL)
    if m:
        smt = re.search(r'  m_StateMachineTransitions:(.*?)  m_StateMachineBehaviours:', m.group(1), re.DOTALL)
        if smt:
            print(f'\n{name} SMT sample:')
            print(smt.group(0)[:250])

with open(F, 'w', encoding='utf-8') as f:
    f.write(content)
print('\nFile saved.')
