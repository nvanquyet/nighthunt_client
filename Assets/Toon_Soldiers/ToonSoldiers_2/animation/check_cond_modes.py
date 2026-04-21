import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F, 'r', encoding='utf-8') as f:
    content = f.read()

# Check full conditions of Handgun_UB AnyState transitions
refs = [9202000,9202001,9202002,9202003,9202004,9202005,
        9202006,9202007,9202008,9202009,9202010,9202011,
        9202012,9202013,9202014,9202015,9202016,9202017,
        9202018,9202019,9202020]

# ConditionMode mapping
MODE = {1:'If(true)', 2:'IfNot(false)', 3:'Greater', 4:'Less', 5:'Equals', 6:'NotEqual', 7:'IfNot?', 9:'Trigger'}

for ref in refs:
    tb = re.search(rf'--- !u!1101 &{ref}\b(.*?)--- !u!', content, re.DOTALL)
    if not tb:
        print(f'&{ref}: NOT FOUND')
        continue
    block = tb.group(1)
    dst_m = re.search(r'm_DstState: \{fileID: (\d+)\}', block)
    isexit = re.search(r'm_IsExit: (\d)', block)
    dst = dst_m.group(1) if dst_m else '0'
    ex = isexit.group(1) if isexit else '?'
    
    # Extract all conditions
    cond_blocks = re.findall(r'm_ConditionEvent: (\S+)\s+m_EventTreshold: \S+\s+m_ConditionMode: (\d+)', block)
    cond_str = ', '.join([f'{name}={MODE.get(int(m),"?"+m)}' for name,m in cond_blocks])
    print(f'&{ref} -> dst:{dst} exit:{ex} | {cond_str}')
