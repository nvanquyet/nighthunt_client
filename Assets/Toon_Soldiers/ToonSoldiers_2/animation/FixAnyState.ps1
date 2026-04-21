$f = "w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller"
$lines = [System.IO.File]::ReadAllLines($f)

# ── Step 1: Build fileID → stateName map ──────────────────────────────────────
$stateMap = @{}
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^--- !u!1102 &(\d+)') {
        $fid = $Matches[1]
        for ($j = $i+1; $j -lt ([Math]::Min($i+12, $lines.Count)); $j++) {
            if ($lines[$j] -match '^\s+m_Name:\s*(.*)$') {
                $stateMap[$fid] = $Matches[1].Trim()
                break
            }
        }
    }
}

# ── Step 2: Find max fileID for generating new ones ───────────────────────────
$maxFid = 0
foreach ($line in $lines) {
    if ($line -match '^--- !u!\d+ &(\d+)') {
        $v = [int]$Matches[1]
        if ($v -gt $maxFid) { $maxFid = $v }
    }
}
$fidCounter = $maxFid + 2
function NewFid { $script:fidCounter += 2; return $script:fidCounter - 2 }

# ── Step 3: Helper to build AnimatorTransition YAML lines ────────────────────
function MakeTransition([int]$tfid, [int]$dstFid, [string[]]$condLines) {
    $r = [System.Collections.Generic.List[string]]::new()
    $r.Add("--- !u!1109 &$tfid")
    $r.Add("AnimatorTransition:")
    $r.Add("  m_ObjectHideFlags: 1")
    $r.Add("  m_CorrespondingSourceObject: {fileID: 0}")
    $r.Add("  m_PrefabInstance: {fileID: 0}")
    $r.Add("  m_PrefabAsset: {fileID: 0}")
    $r.Add("  m_Name: ")
    $r.Add("  m_Conditions:")
    foreach ($cl in $condLines) { $r.Add($cl) }
    $r.Add("  m_DstStateMachine: {fileID: 0}")
    $r.Add("  m_DstState: {fileID: $dstFid}")
    $r.Add("  m_Solo: 0")
    $r.Add("  m_Mute: 0")
    $r.Add("  m_IsExit: 0")
    $r.Add("  serializedVersion: 1")
    return $r
}

function TriggerCond([string]$param) {
    return @(
        "  - m_ConditionMode: 9",
        "    m_ConditionEvent: $param",
        "    m_EventTreshold: 0"
    )
}

function BoolTrueCond([string]$param) {
    return @(
        "  - m_ConditionMode: 1",
        "    m_ConditionEvent: $param",
        "    m_EventTreshold: 0"
    )
}

function IntEqCond([string]$param, [int]$val) {
    return @(
        "  - m_ConditionMode: 6",
        "    m_ConditionEvent: $param",
        "    m_EventTreshold: $val"
    )
}

# ── Step 4: Process each _UpperBody sub-state machine ─────────────────────────
$newTransitionLines = [System.Collections.Generic.List[string]]::new()
# lineIndex -> replacement lines (for m_AnyStateTransitions)
$linePatches = @{}

$targetMachines = @("Handgun_UpperBody","Infantry_UpperBody","Heavy_UpperBody",
                    "Knife_UpperBody","Machinegun_UpperBody","RocketLauncher_UpperBody")

for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -notmatch '^--- !u!1107 &') { continue }

    # Read machine name (within next 5 lines)
    $machineName = ""
    for ($j = $i+1; $j -lt [Math]::Min($i+6,$lines.Count); $j++) {
        if ($lines[$j] -match '^\s+m_Name:\s*(.+)$') { $machineName = $Matches[1].Trim(); break }
    }
    if ($machineName -notin $targetMachines) { continue }

    # Scan sub-machine block until next --- !u! marker
    $childFids   = [System.Collections.Generic.List[string]]::new()
    $anyStateLine = -1
    $inChild      = $false
    for ($j = $i+1; $j -lt $lines.Count; $j++) {
        if ($j -gt $i+1 -and $lines[$j] -match '^--- !u!') { break }
        if ($lines[$j] -match '^\s+m_ChildStates:')         { $inChild = $true; continue }
        if ($inChild -and $lines[$j] -match 'm_State: \{fileID: (\d+)\}') { $childFids.Add($Matches[1]); continue }
        if ($inChild -and $lines[$j] -match '^\s+m_ChildStateMachines:')  { $inChild = $false }
        if ($lines[$j] -match '^\s+m_AnyStateTransitions:')              { $anyStateLine = $j }
    }

    # Build name→fid map for this machine
    $nf = @{}
    foreach ($csfid in $childFids) {
        if ($stateMap.ContainsKey($csfid)) { $nf[$stateMap[$csfid]] = [int]$csfid }
    }

    $newFids = [System.Collections.Generic.List[int]]::new()

    # Helper closure: add trigger transition if target state exists
    $addTrigger = {
        param([string]$trigger, [string]$stateName, [string[]]$extraConds = @())
        if ($nf.ContainsKey($stateName)) {
            $tfid = NewFid
            $conds = (TriggerCond $trigger) + $extraConds
            $newTransitionLines.AddRange((MakeTransition $tfid $nf[$stateName] $conds))
            $newFids.Add($tfid)
        }
    }

    $addBool = {
        param([string]$boolParam, [string]$stateName)
        if ($nf.ContainsKey($stateName)) {
            $tfid = NewFid
            $newTransitionLines.AddRange((MakeTransition $tfid $nf[$stateName] (BoolTrueCond $boolParam)))
            $newFids.Add($tfid)
        }
    }

    # ---- Universal triggers (all weapons) ----
    & $addTrigger "Draw"         "Draw_Stand"
    & $addTrigger "Shoot"        "Shoot_Stand"
    & $addTrigger "ShootBurst"   "ShootBurst_Stand"
    & $addTrigger "Reload"       "Reload_Stand"
    & $addTrigger "ThrowGrenade" "Grenade_Stand"
    & $addTrigger "TakeDamage"   "Damage_Stand"

    # ---- Interact A / B ----
    if ($nf.ContainsKey("Interact_A")) {
        $tfid = NewFid
        $conds = (TriggerCond "Interact") + (IntEqCond "InteractIndex" 0)
        $newTransitionLines.AddRange((MakeTransition $tfid $nf["Interact_A"] $conds))
        $newFids.Add($tfid)
    }
    if ($nf.ContainsKey("Interact_B")) {
        $tfid = NewFid
        $conds = (TriggerCond "Interact") + (IntEqCond "InteractIndex" 1)
        $newTransitionLines.AddRange((MakeTransition $tfid $nf["Interact_B"] $conds))
        $newFids.Add($tfid)
    }

    # ---- Attack A / B (Knife only) ----
    if ($nf.ContainsKey("Attack_A")) {
        $tfid = NewFid
        $conds = (TriggerCond "Attack") + (IntEqCond "AttackIndex" 0)
        $newTransitionLines.AddRange((MakeTransition $tfid $nf["Attack_A"] $conds))
        $newFids.Add($tfid)
    }
    if ($nf.ContainsKey("Attack_B")) {
        $tfid = NewFid
        $conds = (TriggerCond "Attack") + (IntEqCond "AttackIndex" 1)
        $newTransitionLines.AddRange((MakeTransition $tfid $nf["Attack_B"] $conds))
        $newFids.Add($tfid)
    }

    # ---- ShootLoop bool (Heavy, Machinegun) ----
    & $addBool "ShootLoop" "ShootLoop_Stand"

    # ---- ShootBolt / ShootShotgun (Infantry) ----
    & $addBool "ShootBolt"    "ShootBolt_Stand"
    & $addBool "ShootShotgun" "ShootShotgun_Stand"

    # ---- Patch: record replacement for m_AnyStateTransitions line ----
    if ($anyStateLine -ge 0 -and $newFids.Count -gt 0) {
        $patchLines = [System.Collections.Generic.List[string]]::new()
        $patchLines.Add("  m_AnyStateTransitions:")
        foreach ($tfid in $newFids) { $patchLines.Add("  - {fileID: $tfid}") }
        $linePatches[$anyStateLine] = $patchLines
    }

    Write-Host "[$machineName] +$($newFids.Count) AnyState transitions"
}

# ── Step 5: Rebuild file with patches + new blocks appended ──────────────────
$result = [System.Collections.Generic.List[string]]::new()
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($linePatches.ContainsKey($i)) {
        $result.AddRange($linePatches[$i])
    } else {
        $result.Add($lines[$i])
    }
}
# Append all new AnimatorTransition blocks
$result.AddRange($newTransitionLines)

[System.IO.File]::WriteAllLines($f, $result, [System.Text.Encoding]::UTF8)
Write-Host ""
Write-Host "Done! Total new transition blocks: $($newTransitionLines.Count / 16)"
Write-Host "New HasExitTime=0 count: $(($result | Where-Object { $_ -match 'm_AnyStateTransitions:' -and $_ -notmatch '\[\]' }).Count) patched machines"
