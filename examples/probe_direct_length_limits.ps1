param(
    [string]$PlcHost = "192.168.250.100",
    [int]$Port = 1025,
    [ValidateSet("tcp", "udp")]
    [string]$Protocol = "tcp",
    [Parameter(Mandatory = $true)]
    [string]$Profile,
    [string]$Cases = "P1-D0000:622:623:0x4100,U00000:621:622:0x4200,U08000:621:622:0x4300,EB00000:621:622:0x4400",
    [string]$SummaryJson = "logs\\direct_length_limit_pc10g_rerun\\summary.json",
    [double]$TimeoutSeconds = 5.0,
    [int]$ReconnectDelayMs = 300,
    [int]$ChunkSize = 256
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ProjectPath = Join-Path $RepoRoot "examples\\Toyopuc.WriteLimitProbe"
$SummaryPath = Join-Path $RepoRoot $SummaryJson

$command = @(
    "run",
    "--project", $ProjectPath,
    "--",
    "--host", $PlcHost,
    "--port", $Port,
    "--protocol", $Protocol,
    "--profile", $Profile,
    "--cases", $Cases,
    "--summary-json", $SummaryPath,
    "--timeout", $TimeoutSeconds.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    "--reconnect-delay-ms", $ReconnectDelayMs,
    "--chunk-size", $ChunkSize
)

Write-Host ("dotnet " + ($command -join " "))
& dotnet @command
if ($LASTEXITCODE -ne 0) {
    throw "Direct length-limit probe failed."
}
