param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

function Assert-Contains {
    param(
        [string]$Name,
        [string]$Text,
        [string]$Needle
    )

    if (-not $Text.Contains($Needle)) {
        throw "Missing guard '$Needle' in $Name"
    }
}

$predictionPath = Join-Path $Root 'Assets/FishNet/Runtime/Object/NetworkBehaviour/NetworkBehaviour.Prediction.cs'
$serverManagerPath = Join-Path $Root 'Assets/FishNet/Runtime/Managing/Server/ServerManager.cs'

$prediction = Get-Content -LiteralPath $predictionPath -Raw
$serverManager = Get-Content -LiteralPath $serverManagerPath -Raw

$replicateIndex = $prediction.IndexOf('PacketId.Replicate')
$statsGuardIndex = $prediction.IndexOf('if (_networkTrafficStatistics != null)', [Math]::Max(0, $replicateIndex - 500))
$statsCallIndex = $prediction.IndexOf('_networkTrafficStatistics.AddInboundPacketIdData(', [Math]::Max(0, $replicateIndex - 500))

if ($replicateIndex -lt 0 -or $statsGuardIndex -lt 0 -or $statsCallIndex -lt 0 -or $statsGuardIndex -gt $statsCallIndex) {
    throw 'Replicate inbound traffic statistics are not null guarded. Release hosts can kick clients on valid Replicate packets.'
}

Assert-Contains 'ServerManager.cs' $serverManager 'FISHNET_SERVER_PARSE_EXCEPTION'
Assert-Contains 'ServerManager.cs' $serverManager 'recoverableRelayReplicate'
Assert-Contains 'ServerManager.cs' $serverManager 'packetId == PacketId.Replicate'
Assert-Contains 'ServerManager.cs' $serverManager 'e is NullReferenceException'
Assert-Contains 'ServerManager.cs' $serverManager 'exception={e}'
Assert-Contains 'ServerManager.cs' $serverManager 'FISHNET_SERVER_REPLICATE_PARSE_DROPPED'
Assert-Contains 'ServerManager.cs' $serverManager 'action=drop_bundle_keep_connection'

Write-Host 'PASS relay release guard source checks'
