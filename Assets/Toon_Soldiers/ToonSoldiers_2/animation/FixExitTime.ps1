$f = "w:\Unity\Shotter\NightHuntClient\Assets\Toon_Soldiers\ToonSoldiers_2\animation\SoldierAnimatorController.controller"
$lines = [System.IO.File]::ReadAllLines($f)
$fixedTo0 = 0
$fixedTo1 = 0

$i = 0
while ($i -lt $lines.Count) {
    if ($lines[$i] -match '^--- !u!1101') {
        $conditionsEmpty = $true
        $exitTimeNonZero = $false
        $hasExitTimeLine = -1
        $j = $i + 1
        while ($j -lt $lines.Count -and $lines[$j] -notmatch '^--- !u!') {
            $t = $lines[$j].Trim()
            if ($t -eq 'm_Conditions: []') { $conditionsEmpty = $true }
            if ($t -match '^- m_ConditionMode:') { $conditionsEmpty = $false }
            if ($t -match '^m_ExitTime: (0\.[1-9]|[1-9])') { $exitTimeNonZero = $true }
            if ($t -match '^m_HasExitTime:') { $hasExitTimeLine = $j }
            $j++
        }
        if ($hasExitTimeLine -ge 0) {
            if (-not $conditionsEmpty) {
                # Has conditions -> condition-driven, must NOT use exit time
                if ($lines[$hasExitTimeLine] -match 'm_HasExitTime: 1') {
                    $lines[$hasExitTimeLine] = $lines[$hasExitTimeLine] -replace 'm_HasExitTime: 1', 'm_HasExitTime: 0'
                    $fixedTo0++
                }
            } else {
                # No conditions -> timed only; non-zero exit time needs HasExitTime: 1
                if ($exitTimeNonZero -and $lines[$hasExitTimeLine] -match 'm_HasExitTime: 0') {
                    $lines[$hasExitTimeLine] = $lines[$hasExitTimeLine] -replace 'm_HasExitTime: 0', 'm_HasExitTime: 1'
                    $fixedTo1++
                }
            }
        }
        $i = $j
    } else {
        $i++
    }
}

[System.IO.File]::WriteAllLines($f, $lines, [System.Text.Encoding]::UTF8)
Write-Host "Fixed HasExitTime 1->0 (condition-driven): $fixedTo0"
Write-Host "Fixed HasExitTime 0->1 (timed, no conditions): $fixedTo1"
Write-Host "Total HasExitTime=0: $(($lines | Where-Object { $_ -match 'm_HasExitTime: 0' }).Count)"
Write-Host "Total HasExitTime=1: $(($lines | Where-Object { $_ -match 'm_HasExitTime: 1' }).Count)"
