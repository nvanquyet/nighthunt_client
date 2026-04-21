"""
Fix action state exit transitions:
- m_HasExitTime: 0  ->  m_HasExitTime: 1
- Add m_ExitTime: 0.95
Only for transitions that are:
  1. m_IsExit: 1
  2. m_Conditions: []  (no conditions — these are action→Exit, not WeaponChanged→Exit)
  3. NOT referenced in any AnyState list (to avoid touching WeaponChanged exits)

AnyState WeaponChanged exit FIDs (do NOT touch these):
  9202020, 9202043, 9202067, 9202090, 9202114, 9202132, 9202213
  9202133, 9202134, 9202135, 9202136, 9202137, 9202138, 9202203
"""
import re

F = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
with open(F, 'r', encoding='utf-8') as f:
    content = f.read()

# FIDs to skip (WeaponChanged exits)
SKIP_FIDS = {
    9202020, 9202043, 9202067, 9202090, 9202114, 9202132, 9202213,
    9202133, 9202134, 9202135, 9202136, 9202137, 9202138, 9202203,
}

# Find all AnimatorStateTransition blocks
pattern = re.compile(r'(--- !u!1101 &(\d+)\n.*?(?=--- !u!|\Z))', re.DOTALL)

fixed = 0
skipped_anystate = 0
skipped_has_cond = 0
skipped_already_fixed = 0

def fix_block(m):
    global fixed, skipped_anystate, skipped_has_cond, skipped_already_fixed
    block_text = m.group(1)
    fid = int(m.group(2))
    
    if fid in SKIP_FIDS:
        skipped_anystate += 1
        return block_text
    
    # Only fix if IsExit=1
    if 'm_IsExit: 1' not in block_text:
        return block_text
    
    # Only fix if no conditions
    conds = re.findall(r'm_ConditionEvent:', block_text)
    if conds:
        skipped_has_cond += 1
        return block_text
    
    # Already fixed?
    if 'm_HasExitTime: 1' in block_text:
        skipped_already_fixed += 1
        return block_text
    
    # Fix: replace m_HasExitTime: 0 with m_HasExitTime: 1 and set ExitTime
    new_block = block_text.replace('m_HasExitTime: 0', 'm_HasExitTime: 1', 1)
    # Update m_ExitTime to 0.95 (play 95% of animation then exit)
    new_block = re.sub(r'm_ExitTime: \S+', 'm_ExitTime: 0.95', new_block, count=1)
    
    fixed += 1
    return new_block

new_content = pattern.sub(fix_block, content)

print(f'Fixed: {fixed}')
print(f'Skipped (AnyState exits): {skipped_anystate}')
print(f'Skipped (has conditions): {skipped_has_cond}')
print(f'Skipped (already HasExitTime=1): {skipped_already_fixed}')

if fixed > 0:
    with open(F, 'w', encoding='utf-8') as f:
        f.write(new_content)
    print('File saved.')
else:
    print('Nothing to fix.')
