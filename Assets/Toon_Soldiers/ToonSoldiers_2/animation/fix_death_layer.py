"""
Fix Death layer:
1. Death_Empty -> Death_Prone/A/B/C/D/E (Die trigger + IsProne/DeathIndex)
2. Each Death state -> Exit (Respawn trigger, hold until then)
FIDs start at 9204000.
"""
import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F, 'r', encoding='utf-8') as f:
    content = f.read()

# (sm_fid, death_empty_fid, name, [(state_fid, state_name, death_index or -1_for_prone)])
DEATH_MACHINES = [
    (9200111, 9200112, 'Handgun', [
        (9200113, 'Death_A', 0),
        (9200115, 'Death_B', 1),
        (9200117, 'Death_C', 2),
        (9200119, 'Death_D', 3),
        (9200121, 'Death_E', 4),
        (9200123, 'Death_Prone', -1),
    ]),
    (9200243, 9200244, 'Infantry', [
        (9200245, 'Death_A', 0),
        (9200247, 'Death_B', 1),
        (9200249, 'Death_C', 2),
        (9200251, 'Death_D', 3),
        (9200253, 'Death_E', 4),
        (9200255, 'Death_Prone', -1),
    ]),
    (9200378, 9200379, 'Heavy', [
        (9200380, 'Death_A', 0),
        (9200382, 'Death_B', 1),
        (9200384, 'Death_C', 2),
        (9200386, 'Death_D', 3),
        (9200388, 'Death_E', 4),
        (9200390, 'Death_Prone', -1),
    ]),
    (9200510, 9200511, 'Knife', [
        (9200512, 'Death_A', 0),
        (9200514, 'Death_B', 1),
        (9200516, 'Death_C', 2),
        (9200518, 'Death_D', 3),
        (9200520, 'Death_E', 4),
        (9200522, 'Death_Prone', -1),
    ]),
    (9200645, 9200646, 'Machinegun', [
        (9200647, 'Death_A', 0),
        (9200649, 'Death_B', 1),
        (9200651, 'Death_C', 2),
        (9200653, 'Death_D', 3),
        (9200655, 'Death_E', 4),
        (9200657, 'Death_Prone', -1),
    ]),
    (9200762, 9200763, 'RocketLauncher', [
        (9200764, 'Death_A', 0),
        (9200766, 'Death_B', 1),
        (9200768, 'Death_C', 2),
        (9200770, 'Death_Prone', -1),
    ]),
    (9202220, 9202221, 'Unarmed', [
        (9202222, 'Death_A', 0),
        (9202223, 'Death_B', 1),
        (9202224, 'Death_C', 2),
        (9202225, 'Death_D', 3),
        (9202226, 'Death_E', 4),
        (9202227, 'Death_Prone', -1),
    ]),
]

def make_transition(fid, dst_fid, conditions, is_exit=False, has_exit_time=0, exit_time=0.95):
    """
    conditions: list of (m_ConditionMode, m_ConditionEvent, m_EventTreshold)
    ConditionMode: 1=If(trigger/bool-true), 2=IfNot(bool-false), 5=Equals(int)
    """
    cond_yaml = ''
    for mode, event, thresh in conditions:
        cond_yaml += f'  - m_ConditionMode: {mode}\n    m_ConditionEvent: {event}\n    m_EventTreshold: {thresh}\n'
    if not cond_yaml:
        cond_yaml = ''  # empty list

    dst_state_line = f'  m_DstState: {{fileID: {dst_fid}}}\n' if not is_exit else '  m_DstState: {fileID: 0}\n'
    is_exit_val = 1 if is_exit else 0

    return (
        f'--- !u!1101 &{fid}\n'
        f'AnimatorStateTransition:\n'
        f'  m_ObjectHideFlags: 1\n'
        f'  m_CorrespondingSourceObject: {{fileID: 0}}\n'
        f'  m_PrefabInstance: {{fileID: 0}}\n'
        f'  m_PrefabAsset: {{fileID: 0}}\n'
        f'  m_Name: \n'
        f'  m_Conditions:\n'
        f'{cond_yaml}'
        f'  m_DstStateMachine: {{fileID: 0}}\n'
        f'{dst_state_line}'
        f'  m_Solo: 0\n'
        f'  m_Mute: 0\n'
        f'  m_IsExit: {is_exit_val}\n'
        f'  serializedVersion: 3\n'
        f'  m_TransitionDuration: 0.1\n'
        f'  m_TransitionOffset: 0\n'
        f'  m_ExitTime: {exit_time}\n'
        f'  m_HasExitTime: {has_exit_time}\n'
        f'  m_HasFixedDuration: 1\n'
        f'  m_InterruptionSource: 0\n'
        f'  m_OrderedInterruption: 1\n'
        f'  m_CanTransitionToSelf: 0\n'
    )

def patch_m_transitions(content, state_fid, trans_fids):
    """Replace m_Transitions: [] on a state with list of FIDs."""
    trans_list = ''.join([f'  - {{fileID: {t}}}\n' for t in trans_fids])
    pattern = rf'(--- !u!1102 &{state_fid}\b.*?m_Transitions:) \[\](.*?m_StateMachineBehaviours:)'
    replacement = rf'\g<1>\n{trans_list}\g<2>'
    new, n = re.subn(pattern, replacement, content, count=1, flags=re.DOTALL)
    return new, n > 0

next_fid = 9204000
new_blocks = []
new_content = content

total_enter = 0
total_respawn = 0

for sm_fid, death_empty_fid, weapon_name, states in DEATH_MACHINES:
    print(f'\n{weapon_name} (Death_Empty:{death_empty_fid}):')
    enter_fids = []  # transitions on Death_Empty -> action states

    # Prone first (higher priority)
    prone_states = [(f, n, idx) for f, n, idx in states if idx == -1]
    indexed_states = sorted([(f, n, idx) for f, n, idx in states if idx >= 0], key=lambda x: x[2])

    for state_fid, state_name, death_index in prone_states + indexed_states:
        # --- Transition: Death_Empty -> this state ---
        enter_fid = next_fid; next_fid += 1
        if death_index == -1:
            # Prone death
            conds = [(1, 'Die', 0), (1, 'IsProne', 0)]
        else:
            # Indexed death, must not be prone
            conds = [(1, 'Die', 0), (5, 'DeathIndex', death_index), (2, 'IsProne', 0)]
        enter_fids.append(enter_fid)
        new_blocks.append(make_transition(enter_fid, state_fid, conds, is_exit=False, has_exit_time=0))
        print(f'  &{enter_fid}: Death_Empty -> {state_name} | {[e for _,e,_ in conds]}')
        total_enter += 1

        # --- Transition: this state -> Exit (Respawn trigger, hold until then) ---
        respawn_fid = next_fid; next_fid += 1
        respawn_conds = [(1, 'Respawn', 0)]
        new_blocks.append(make_transition(respawn_fid, 0, respawn_conds, is_exit=True, has_exit_time=0))
        # Patch m_Transitions on death state
        new_content, ok = patch_m_transitions(new_content, state_fid, [respawn_fid])
        print(f'  &{respawn_fid}: {state_name} -> Exit (Respawn) | patched={ok}')
        total_respawn += 1

    # Patch m_Transitions on Death_Empty
    new_content, ok = patch_m_transitions(new_content, death_empty_fid, enter_fids)
    print(f'  Death_Empty({death_empty_fid}) patched={ok} ({len(enter_fids)} outgoing)')

print(f'\nTotal enter transitions: {total_enter}')
print(f'Total respawn exits: {total_respawn}')
print(f'Total new blocks: {len(new_blocks)}')

# Insert all new blocks at end of file
insertion_point = new_content.rfind('--- !u!')
new_content = new_content[:insertion_point] + ''.join(new_blocks) + new_content[insertion_point:]

with open(F, 'w', encoding='utf-8') as f:
    f.write(new_content)
print('File saved.')
