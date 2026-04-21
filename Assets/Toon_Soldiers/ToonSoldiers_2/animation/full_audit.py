"""
Full audit of SoldierAnimatorController:
- Base Layer root machine & all sub-machines
- UpperBody layer
- Death layer
- All Entry/AnyState/Exit transitions
- State list per machine
"""
import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F,'r',encoding='utf-8') as f: lines=f.readlines()

# Build fid->name and fid->type maps
fid_name = {}
fid_type = {}
for i,l in enumerate(lines):
    m = re.match(r'^--- !u!(\d+) &(\d+)', l)
    if m:
        utype, fid = m.group(1), m.group(2)
        fid_type[fid] = utype
        # Find m_Name
        for j in range(i+1, min(i+12, len(lines))):
            if re.match(r'^--- !u!', lines[j]): break
            nm = re.match(r'\s+m_Name: (.*)', lines[j])
            if nm:
                fid_name[fid] = nm.group(1).strip()
                break

def name(fid):
    return fid_name.get(fid, f'?{fid}')

def get_machine(fid):
    """Returns dict with keys: name, child_states, child_machines, default_state,
       any_transitions, entry_transitions, statemachine_transitions"""
    result = dict(name='', child_states=[], child_machines=[],
                  default_state=None, any_transitions=[], entry_transitions=[],
                  sm_transitions={})
    for i,l in enumerate(lines):
        if re.match(r'^--- !u!1107 &' + fid + r'\b', l):
            result['name'] = fid_name.get(fid,'')
            section = None
            j = i+1
            while j < len(lines) and not re.match(r'^--- !u!', lines[j]):
                stripped = lines[j].strip()
                if 'm_ChildStates:' in lines[j]: section='states'
                elif 'm_ChildStateMachines:' in lines[j]: section='machines'
                elif 'm_AnyStateTransitions:' in lines[j]: section='any'
                elif 'm_EntryTransitions:' in lines[j]: section='entry'
                elif 'm_StateMachineTransitions:' in lines[j]: section='smtrans'
                elif 'm_DefaultState:' in lines[j]:
                    mm = re.search(r'fileID: (\d+)', lines[j])
                    if mm: result['default_state'] = mm.group(1)
                
                if stripped.startswith('- {fileID:') or stripped.startswith('- serializedVersion:'):
                    mm = re.search(r'fileID: (\d+)', lines[j])
                    if mm and mm.group(1) != '0':
                        fref = mm.group(1)
                        if section == 'states' and fid_type.get(fref) == '1102':
                            result['child_states'].append(fref)
                        elif section == 'machines':
                            pass  # handled by m_StateMachine line below
                        elif section == 'any':
                            result['any_transitions'].append(fref)
                        elif section == 'entry':
                            result['entry_transitions'].append(fref)
                
                if 'm_StateMachine:' in lines[j] and section == 'machines':
                    mm = re.search(r'fileID: (\d+)', lines[j])
                    if mm and mm.group(1) != '0':
                        result['child_machines'].append(mm.group(1))
                
                j += 1
            break
    return result

def get_transition(fid):
    result = dict(type=fid_type.get(fid,'?'), dst_state=None, dst_machine=None,
                  conditions=[], is_exit=False)
    for i,l in enumerate(lines):
        if re.match(r'^--- !u!\d+ &' + fid + r'\b', l):
            j = i+1
            cond = {}
            while j < len(lines) and not re.match(r'^--- !u!', lines[j]):
                if 'm_DstState:' in lines[j]:
                    mm = re.search(r'fileID: (\d+)', lines[j])
                    if mm: result['dst_state'] = mm.group(1)
                if 'm_DstStateMachine:' in lines[j]:
                    mm = re.search(r'fileID: (\d+)', lines[j])
                    if mm: result['dst_machine'] = mm.group(1)
                if 'm_IsExit:' in lines[j]:
                    result['is_exit'] = '1' in lines[j]
                if 'm_ConditionMode:' in lines[j]:
                    cond = {'mode': lines[j].split(':')[1].strip()}
                if 'm_ConditionEvent:' in lines[j]:
                    cond['event'] = lines[j].split(':')[1].strip()
                if 'm_EventTreshold:' in lines[j] and cond:
                    cond['thresh'] = lines[j].split(':')[1].strip()
                    result['conditions'].append(dict(cond))
                    cond = {}
                j += 1
            break
    return result

def print_transitions(trans_fids, indent='    '):
    for tfid in trans_fids:
        t = get_transition(tfid)
        dst = name(t['dst_state']) if t['dst_state'] and t['dst_state']!='0' else ''
        dst_m = name(t['dst_machine']) if t['dst_machine'] and t['dst_machine']!='0' else ''
        cond_str = ', '.join(f"{c['event']}(mode={c['mode']},thr={c.get('thresh','?')})" for c in t['conditions'])
        target = f"->{dst_m}/{dst}" if dst_m else f"->{dst}"
        exit_flag = ' [EXIT]' if t['is_exit'] else ''
        print(f"{indent}&{tfid} {cond_str} {target}{exit_flag}")

# ─── Get layers ───
print("=" * 70)
print("LAYERS")
print("=" * 70)
layer_machines = {}
for i,l in enumerate(lines):
    if re.match(r'^--- !u!91 &', l):
        j = i+1
        while j < len(lines) and not re.match(r'^--- !u!', lines[j]):
            if 'm_Name: Base Layer' in lines[j]:
                for k in range(j, j+5):
                    mm = re.search(r'm_StateMachine: \{fileID: (\d+)\}', lines[k])
                    if mm: layer_machines['Base Layer'] = mm.group(1)
            if 'm_Name: UpperBody' in lines[j]:
                for k in range(j, j+5):
                    mm = re.search(r'm_StateMachine: \{fileID: (\d+)\}', lines[k])
                    if mm: layer_machines['UpperBody'] = mm.group(1)
            if 'm_Name: Death' in lines[j]:
                for k in range(j, j+5):
                    mm = re.search(r'm_StateMachine: \{fileID: (\d+)\}', lines[k])
                    if mm: layer_machines['Death'] = mm.group(1)
            j += 1
        break

for layer, root_fid in layer_machines.items():
    print(f"\n{'─'*60}")
    print(f"LAYER: {layer}  root=&{root_fid}")
    print(f"{'─'*60}")
    root = get_machine(root_fid)
    print(f"  Direct states: {[name(s) for s in root['child_states']]}")
    print(f"  Sub-machines: {[name(m) for m in root['child_machines']]}")
    print(f"  Default: {name(root['default_state'])}")
    print(f"  AnyState ({len(root['any_transitions'])}):")
    print_transitions(root['any_transitions'])
    print(f"  Entry ({len(root['entry_transitions'])}):")
    print_transitions(root['entry_transitions'])
    
    for sub_fid in root['child_machines']:
        sub = get_machine(sub_fid)
        print(f"\n  SUB-MACHINE: {sub['name']} &{sub_fid}")
        print(f"    States: {[name(s) for s in sub['child_states']]}")
        print(f"    Default: {name(sub['default_state'])}")
        print(f"    AnyState ({len(sub['any_transitions'])}):")
        print_transitions(sub['any_transitions'], '      ')
        print(f"    Entry ({len(sub['entry_transitions'])}):")
        print_transitions(sub['entry_transitions'], '      ')
        
        # Check UB_Empty transitions
        ds = sub['default_state']
        if ds:
            # Get its m_Transitions
            for i2,l2 in enumerate(lines):
                if re.match(r'^--- !u!1102 &' + ds + r'\b', l2):
                    trans = []
                    j = i2+1
                    in_t = False
                    while j < len(lines) and not re.match(r'^--- !u!', lines[j]):
                        if 'm_Transitions:' in lines[j]: in_t=True
                        if in_t and '{fileID:' in lines[j]:
                            mm = re.search(r'fileID: (\d+)', lines[j])
                            if mm and mm.group(1)!='0': trans.append(mm.group(1))
                        j += 1
                    print(f"    Default state '{name(ds)}' transitions ({len(trans)}):")
                    print_transitions(trans[:5], '      ')
                    if len(trans) > 5: print(f"      ...and {len(trans)-5} more")
                    break
