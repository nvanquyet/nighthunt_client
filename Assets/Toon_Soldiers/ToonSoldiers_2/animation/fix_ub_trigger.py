"""
Fix UpperBody weapon switch: separate trigger WeaponChangedUB
Reason: WeaponChanged trigger is consumed by Base Layer first,
so UpperBody never sees it.

Changes:
1. Add WeaponChangedUB trigger parameter (type=9)
2. Update 7 UpperBody AnyState exit transitions:
   change m_ConditionEvent: WeaponChanged -> WeaponChangedUB
   FIDs: 9202020, 9202043, 9202067, 9202090, 9202114, 9202132, 9202213
"""
import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F, 'r', encoding='utf-8') as f:
    content = f.read()

# ── 1. Add WeaponChangedUB parameter if not present ───────────────────────────
if 'WeaponChangedUB' in content:
    print('WeaponChangedUB param already exists')
else:
    # Find existing WeaponChanged param block and insert after it
    # Parameters are listed as: - m_Name: WeaponChanged\n    m_Type: 9
    insert_after = re.search(
        r'(  - m_Name: WeaponChanged\s+m_Type: 9[^\n]*\n(?:    [^\n]+\n)*)',
        content
    )
    if insert_after:
        new_param = (
            '  - m_Name: WeaponChangedUB\n'
            '    m_Type: 9\n'
            '    m_DefaultFloat: 0\n'
            '    m_DefaultInt: 0\n'
            '    m_DefaultBool: 0\n'
            '    m_Controller: {fileID: 0}\n'
        )
        pos = insert_after.end()
        content = content[:pos] + new_param + content[pos:]
        print('Added WeaponChangedUB parameter')
    else:
        # Fallback: find m_Parameters list and append
        params_end = re.search(r'(m_Parameters:.*?)(m_AnimatorLayers:)', content, re.DOTALL)
        if params_end:
            new_param = (
                '  - m_Name: WeaponChangedUB\n'
                '    m_Type: 9\n'
                '    m_DefaultFloat: 0\n'
                '    m_DefaultInt: 0\n'
                '    m_DefaultBool: 0\n'
                '    m_Controller: {fileID: 0}\n'
            )
            insert_pos = params_end.start(2)
            content = content[:insert_pos] + new_param + content[insert_pos:]
            print('Added WeaponChangedUB parameter (fallback method)')
        else:
            print('ERROR: cannot find parameter list')

# ── 2. Update 7 UpperBody AnyState exit transitions ───────────────────────────
UB_EXIT_FIDS = [9202020, 9202043, 9202067, 9202090, 9202114, 9202132, 9202213]

updated = 0
for fid in UB_EXIT_FIDS:
    # Find block
    m = re.search(rf'(--- !u!1101 &{fid}\b.*?)(--- !u!)', content, re.DOTALL)
    if not m:
        print(f'WARNING: &{fid} not found')
        continue

    block = m.group(1)
    if 'WeaponChangedUB' in block:
        print(f'&{fid}: already uses WeaponChangedUB')
        continue
    if 'WeaponChanged' not in block:
        print(f'WARNING: &{fid} has no WeaponChanged condition')
        continue

    # Replace in this block only
    new_block = block.replace('m_ConditionEvent: WeaponChanged', 'm_ConditionEvent: WeaponChangedUB', 1)
    content = content[:m.start()] + new_block + content[m.start()+len(m.group(1)):]
    print(f'&{fid}: WeaponChanged -> WeaponChangedUB')
    updated += 1

print(f'\nUpdated {updated}/{len(UB_EXIT_FIDS)} UpperBody exit transitions')

# ── Verify ────────────────────────────────────────────────────────────────────
print('\nVerification:')
for fid in UB_EXIT_FIDS:
    m = re.search(rf'--- !u!1101 &{fid}\b(.*?)--- !u!', content, re.DOTALL)
    if m:
        cond_ev = re.search(r'm_ConditionEvent: (\S+)', m.group(1))
        is_exit = re.search(r'm_IsExit: (\d)', m.group(1))
        print(f'  &{fid}: cond={cond_ev.group(1) if cond_ev else "?"} exit={is_exit.group(1) if is_exit else "?"}')

with open(F, 'w', encoding='utf-8') as f:
    f.write(content)
print('\nFile saved.')
