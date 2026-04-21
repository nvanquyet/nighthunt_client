import re
with open('SoldierAnimatorController.controller','r',encoding='utf-8') as f: c=f.read()

# Check what the old AnyState transitions in Death SMs are doing
# If they also use Die trigger -> they duplicate Death_Empty transitions -> potential conflict
print('=== Handgun_Death AnyState transition details ===')
for fid in ['9200114', '9200116', '9200118', '9200120', '9200122', '9200124', '9200125']:
    tb = re.search(rf'--- !u!1101 &{fid}\b(.*?)--- !u!', c, re.DOTALL)
    if tb:
        cond_ev = re.findall(r'm_ConditionEvent: (\S+)', tb.group(1))
        cond_mode = re.findall(r'm_ConditionMode: (\d+)', tb.group(1))
        dst = re.search(r'm_DstState: \{fileID: (\d+)\}', tb.group(1))
        isexit = re.search(r'm_IsExit: (\d)', tb.group(1))
        dst_fid = dst.group(1) if dst else '0'
        # Get dst state name
        if dst_fid != '0':
            sb = re.search(rf'--- !u!1102 &{dst_fid}\b(.*?)--- !u!', c, re.DOTALL)
            nm = re.search(r'm_Name: (.+)', sb.group(1)) if sb else None
            dst_name = nm.group(1).strip() if nm else '?'
        else:
            dst_name = 'EXIT'
        pairs = list(zip(cond_ev, cond_mode))
        print(f'  &{fid}: {pairs} -> {dst_name}({dst_fid}) IsExit={isexit.group(1) if isexit else "?"}')
