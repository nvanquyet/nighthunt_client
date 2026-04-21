"""
Thêm AnyState transitions vào từng weapon UpperBody sub-state machine.
Root cause: generator tạo đủ states nhưng m_AnyStateTransitions = [] ở mọi machine.
"""
import re

F = r"w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller"

with open(F, 'r', encoding='utf-8') as fp:
    raw = fp.read()

# Normalise line endings
raw = raw.replace('\r\n', '\n').replace('\r', '\n')
lines = raw.split('\n')
print(f"Total lines: {len(lines)}")

# ── 1. Build fileID → stateName map ───────────────────────────────────────────
state_map: dict[str, str] = {}
for i, line in enumerate(lines):
    m = re.match(r'^--- !u!1102 &(\d+)', line)
    if m:
        fid = m.group(1)
        for j in range(i + 1, min(i + 12, len(lines))):
            m2 = re.match(r'\s+m_Name:\s*(.*)$', lines[j])
            if m2:
                state_map[fid] = m2.group(1).strip()
                break
print(f"States in map: {len(state_map)}")

# ── 2. Find max fileID ─────────────────────────────────────────────────────────
max_fid = 0
for line in lines:
    m = re.match(r'^--- !u!\d+ &(\d+)', line)
    if m:
        max_fid = max(max_fid, int(m.group(1)))
print(f"Max fileID: {max_fid}")

fid_seq = [max_fid + 2]
def new_fid():
    v = fid_seq[0]; fid_seq[0] += 2; return v

# ── 3. YAML builders ──────────────────────────────────────────────────────────
def make_transition(tfid: int, dst_fid: int, cond_lines: list[str]) -> list[str]:
    block = [
        f"--- !u!1109 &{tfid}",
        "AnimatorTransition:",
        "  m_ObjectHideFlags: 1",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_Name: ",
        "  m_Conditions:",
    ]
    block.extend(cond_lines)
    block.extend([
        "  m_DstStateMachine: {fileID: 0}",
        f"  m_DstState: {{fileID: {dst_fid}}}",
        "  m_Solo: 0",
        "  m_Mute: 0",
        "  m_IsExit: 0",
        "  serializedVersion: 1",
    ])
    return block

def trigger_cond(param: str) -> list[str]:
    return ["  - m_ConditionMode: 9", f"    m_ConditionEvent: {param}", "    m_EventTreshold: 0"]

def bool_true_cond(param: str) -> list[str]:
    return ["  - m_ConditionMode: 1", f"    m_ConditionEvent: {param}", "    m_EventTreshold: 0"]

def int_eq_cond(param: str, val: int) -> list[str]:
    return ["  - m_ConditionMode: 6", f"    m_ConditionEvent: {param}", f"    m_EventTreshold: {val}"]

# ── 4. Process each _UpperBody sub-machine ────────────────────────────────────
TARGET_MACHINES = {
    "Handgun_UpperBody", "Infantry_UpperBody", "Heavy_UpperBody",
    "Knife_UpperBody", "Machinegun_UpperBody", "RocketLauncher_UpperBody"
}

new_transition_lines: list[str] = []
line_patches: dict[int, list[str]] = {}  # line_idx → replacement lines

i = 0
while i < len(lines):
    m = re.match(r'^--- !u!1107 &(\d+)', lines[i])
    if not m:
        i += 1; continue

    # Read machine name (m_Name is ~8 lines after the --- !u!1107 header)
    machine_name = ""
    for j in range(i + 1, min(i + 14, len(lines))):
        m2 = re.match(r'\s+m_Name:\s*(.+)$', lines[j])
        if m2:
            machine_name = m2.group(1).strip(); break

    if machine_name not in TARGET_MACHINES:
        i += 1; continue

    # Scan sub-machine block until next --- !u! marker
    child_fids: list[str] = []
    any_state_line = -1
    in_child = False
    for j in range(i + 1, len(lines)):
        if j > i + 1 and re.match(r'^--- !u!', lines[j]):
            break
        if re.match(r'\s+m_ChildStates:', lines[j]):
            in_child = True; continue
        if in_child:
            ms = re.search(r'm_State: \{fileID: (\d+)\}', lines[j])
            if ms:
                child_fids.append(ms.group(1))
            if re.match(r'\s+m_ChildStateMachines:', lines[j]):
                in_child = False
        if re.match(r'\s+m_AnyStateTransitions:', lines[j]):
            any_state_line = j

    # Build name → int_fid map for this machine
    nf: dict[str, int] = {}
    for csfid in child_fids:
        if csfid in state_map:
            nf[state_map[csfid]] = int(csfid)

    print(f"\n[{machine_name}]  children:{len(child_fids)}  anyLine:{any_state_line}")
    print(f"  States: {sorted(nf.keys())}")

    new_fids: list[int] = []

    def add_t(trigger: str, state: str, extra: list[str] | None = None):
        if state in nf:
            tfid = new_fid()
            conds = trigger_cond(trigger) + (extra or [])
            new_transition_lines.extend(make_transition(tfid, nf[state], conds))
            new_fids.append(tfid)
            print(f"  + {trigger} -> {state}")

    def add_b(param: str, state: str):
        if state in nf:
            tfid = new_fid()
            new_transition_lines.extend(make_transition(tfid, nf[state], bool_true_cond(param)))
            new_fids.append(tfid)
            print(f"  + {param}=true -> {state}")

    # Universal triggers
    add_t("Draw",         "Draw_Stand")
    add_t("Shoot",        "Shoot_Stand")
    add_t("ShootBurst",   "ShootBurst_Stand")
    add_t("Reload",       "Reload_Stand")
    add_t("ThrowGrenade", "Grenade_Stand")
    add_t("TakeDamage",   "Damage_Stand")

    # Interact A/B
    if "Interact_A" in nf:
        tfid = new_fid()
        new_transition_lines.extend(make_transition(tfid, nf["Interact_A"],
            trigger_cond("Interact") + int_eq_cond("InteractIndex", 0)))
        new_fids.append(tfid); print(f"  + Interact[0] -> Interact_A")
    if "Interact_B" in nf:
        tfid = new_fid()
        new_transition_lines.extend(make_transition(tfid, nf["Interact_B"],
            trigger_cond("Interact") + int_eq_cond("InteractIndex", 1)))
        new_fids.append(tfid); print(f"  + Interact[1] -> Interact_B")

    # Attack A/B (Knife)
    if "Attack_A" in nf:
        tfid = new_fid()
        new_transition_lines.extend(make_transition(tfid, nf["Attack_A"],
            trigger_cond("Attack") + int_eq_cond("AttackIndex", 0)))
        new_fids.append(tfid); print(f"  + Attack[0] -> Attack_A")
    if "Attack_B" in nf:
        tfid = new_fid()
        new_transition_lines.extend(make_transition(tfid, nf["Attack_B"],
            trigger_cond("Attack") + int_eq_cond("AttackIndex", 1)))
        new_fids.append(tfid); print(f"  + Attack[1] -> Attack_B")

    # Bool transitions
    add_b("ShootLoop",    "ShootLoop_Stand")
    add_b("ShootBolt",    "ShootBolt_Stand")
    add_b("ShootShotgun", "ShootShotgun_Stand")

    if any_state_line >= 0 and new_fids:
        patch = ["  m_AnyStateTransitions:"]
        patch.extend(f"  - {{fileID: {tfid}}}" for tfid in new_fids)
        line_patches[any_state_line] = patch
        print(f"  => {len(new_fids)} AnyState transitions added")
    elif not new_fids:
        print(f"  WARNING: no transitions generated!")

    i += 1

# ── 5. Rebuild file ───────────────────────────────────────────────────────────
result: list[str] = []
for i, line in enumerate(lines):
    if i in line_patches:
        result.extend(line_patches[i])
    else:
        result.append(line)

result.extend(new_transition_lines)

out = '\n'.join(result)
with open(F, 'w', encoding='utf-8', newline='\n') as fp:
    fp.write(out)

print(f"\n{'='*60}")
print(f"Done. Patched {len(line_patches)} machines, {len(new_transition_lines)} new transition lines")
print(f"New total lines: {len(result)}")
