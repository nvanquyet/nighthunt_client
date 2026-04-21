import re

path = r'w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller'
lines = open(path, encoding='utf-8').readlines()

blocks_1102 = [i for i, l in enumerate(lines) if re.match(r'--- !u!1102 &', l)]

state_info = {}
for b in blocks_1102:
    end = b + 1
    while end < len(lines) and not re.match(r'--- !u!', lines[end]):
        end += 1
    chunk = ''.join(lines[b:end])
    fid = re.search(r'&(\d+)', lines[b]).group(1)
    name = re.search(r'm_Name: (.+)', chunk)
    speed = re.search(r'm_Speed: (.+)', chunk)
    cycle = re.search(r'm_CycleOffset: (.+)', chunk)
    motion = re.search(r'm_Motion: \{fileID: (\d+), guid: ([0-9a-f]+)', chunk)
    state_info[fid] = {
        'name': name.group(1).strip() if name else '',
        'speed': speed.group(1).strip() if speed else '1',
        'cycle': cycle.group(1).strip() if cycle else '0',
        'motion_fid': motion.group(1) if motion else '0',
        'motion_guid': motion.group(2) if motion else '',
        'block_start': b,
        'block_end': end,
    }

print('=== Draw states ===')
for fid, info in sorted(state_info.items(), key=lambda x: int(x[0])):
    if 'Draw' in info['name']:
        print('  [' + fid + '] ' + info['name'] + '  speed=' + info['speed'] + '  motionGUID=' + info['motion_guid'][:20])

print()
print('=== Raw YAML of first Draw_Stand ===')
for b in blocks_1102:
    end = b + 1
    while end < len(lines) and not re.match(r'--- !u!', lines[end]):
        end += 1
    chunk = ''.join(lines[b:end])
    if re.search(r'm_Name: Draw_Stand', chunk):
        print(chunk)
        break

print()
print('=== Raw YAML of first UB_Empty transitions block ===')
# Find first UB_Empty state
for b in blocks_1102:
    end = b + 1
    while end < len(lines) and not re.match(r'--- !u!', lines[end]):
        end += 1
    chunk = ''.join(lines[b:end])
    if re.search(r'm_Name: UB_Empty', chunk):
        print(chunk[:2000])
        break
