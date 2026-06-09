Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$basePath = Join-Path $root "Assets/_Night_Hunt/Scripts/Gameplay/Character/Movement/BaseCharacterPredictedMovement.cs"
$rigidbodyPath = Join-Path $root "Assets/_Night_Hunt/Scripts/Gameplay/Character/Movement/RigidbodyPredictedMovement.cs"
$prefabPath = Join-Path $root "Assets/_Night_Hunt/Prefabs/PlayerPrefab.prefab"

$base = Get-Content -LiteralPath $basePath -Raw
$rigidbody = Get-Content -LiteralPath $rigidbodyPath -Raw
$prefab = Get-Content -LiteralPath $prefabPath -Raw

function Assert-Contains {
    param(
        [string]$Name,
        [string]$Text,
        [string]$Pattern
    )

    if ($Text -notmatch [regex]::Escape($Pattern)) {
        throw "Missing expected source pattern in ${Name}: ${Pattern}"
    }
}

Assert-Contains "BaseCharacterPredictedMovement" $base "protected bool useFishNetGraphicalSmoothingForObservers = true;"
Assert-Contains "BaseCharacterPredictedMovement" $base "NetworkObject.EnableStateForwarding"
Assert-Contains "BaseCharacterPredictedMovement" $base "NetworkObject.GetGraphicalObject() != null"
Assert-Contains "BaseCharacterPredictedMovement" $base "if (ShouldUseFishNetGraphicalSmoothingForObservers())"
Assert-Contains "BaseCharacterPredictedMovement" $base "&& !ShouldUseFishNetGraphicalSmoothingForObservers())"
Assert-Contains "BaseCharacterPredictedMovement" $base "[NH_NET_SMOOTH]"

Assert-Contains "RigidbodyPredictedMovement" $rigidbody "ShouldUseFishNetGraphicalSmoothingForObservers()) return;"
Assert-Contains "RigidbodyPredictedMovement" $rigidbody "protected override void ApplyObserverAuthoritativeTransform(Vector3 position, Quaternion rotation)"
Assert-Contains "RigidbodyPredictedMovement" $rigidbody "_rigidbody.position = position;"
Assert-Contains "RigidbodyPredictedMovement" $rigidbody "_rigidbody.rotation = rotation;"

Assert-Contains "PlayerPrefab" $prefab "useFishNetGraphicalSmoothingForObservers: 1"
Assert-Contains "PlayerPrefab" $prefab "logObserverSmoothingStats: 0"
Assert-Contains "PlayerPrefab" $prefab "_graphicalObject: {fileID: 636343447752430072}"
Assert-Contains "PlayerPrefab" $prefab "_enableStateForwarding: 1"

Write-Host "Observer smoothing source verification passed."
