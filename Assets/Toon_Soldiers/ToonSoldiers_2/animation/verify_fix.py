import re

path = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
content = open(path, encoding='utf-8').read()

# Check 1: Holster parameter exists
print('Holster param:', 'm_Name: Holster' in content)

# Check 2: Holster_Stand state for Handgun (FID 9207000) has speed=-1 and correct motion
m = re.search(r'--- !u!1102 &9207000\n.*?m_Speed: ([\d.-]+).*?m_Motion: \{fileID: (\d+), guid: ([0-9a-f]+)', content, re.DOTALL)
if m:
    print(f'Handgun Holster_Stand: speed={m.group(1)}, guid={m.group(3)}')

# Compare with Draw_Stand guid
m2 = re.search(r'--- !u!1102 &9200050\n.*?m_Motion: \{fileID: (\d+), guid: ([0-9a-f]+)', content, re.DOTALL)
if m2:
    print(f'Handgun Draw_Stand guid:   {m2.group(2)}')
    match = m.group(3) == m2.group(2) if m else False
    print(f'GUIDs match: {match}')

# Check 3: Knife UB_Empty no longer has Shoot/Reload, but has Attack_Prone
ub_start = content.find('--- !u!1102 &9200442\n')
next_blk = content.find('--- !u!', ub_start+10)
ub_block = content[ub_start:next_blk]
print(f'Knife Shoot_Stand (9203066) in UB_Empty: {chr(39)}{9203066 in [int(x) for x in re.findall(r"fileID: (\d+)", ub_block)]}{chr(39)}')
print(f'Knife Attack_Prone (9207300) in UB_Empty: {"9207300" in ub_block}')
print(f'Knife Holster_Stand (9207209) in UB_Empty: {"9207209" in ub_block}')

# Check 4: Unarmed SM has attack states
sm_start = content.find('--- !u!1107 &9202210\n')
next_sm = content.find('--- !u!', sm_start+10)
sm_block = content[sm_start:next_sm]
print(f'Unarmed SM has 9207400: {"9207400" in sm_block}')
print(f'Unarmed SM has 9207404: {"9207404" in sm_block}')

# Check 5: Unarmed UB_Empty has attack transitions
ub2_start = content.find('--- !u!1102 &9202211\n')
next_ub2 = content.find('--- !u!', ub2_start+10)
ub2_block = content[ub2_start:next_ub2]
print(f'Unarmed UB_Empty has 9207420: {"9207420" in ub2_block}')
print(f'Unarmed UB_Empty has 9207424: {"9207424" in ub2_block}')

# Check 6: TransitionOffset=1 on Holster transition
m3 = re.search(r'--- !u!1101 &9207200\n.*?m_TransitionOffset: ([\d.]+)', content, re.DOTALL)
if m3:
    print(f'Holster_Stand trans offset: {m3.group(1)} (want 1)')

# Check 7: Holster exit trans exitTime=0.05
m4 = re.search(r'--- !u!1101 &9207100\n.*?m_ExitTime: ([\d.]+)\n.*?m_HasExitTime: (\d)', content, re.DOTALL)
if m4:
    print(f'Holster exit: exitTime={m4.group(1)}, hasExitTime={m4.group(2)}')

# Check 8: Smoothed transitions
c075 = content.count('m_TransitionDuration: 0.075')
c005 = len(re.findall(r'm_TransitionDuration: 0\.05\b', content))
print(f'Transitions with dur=0.075: {c075}')
print(f'Transitions with dur=0.05 remaining: {c005}')
