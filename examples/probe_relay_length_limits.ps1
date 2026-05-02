param(
    [string]$PlcHost = "192.168.250.100",
    [int]$Port = 1025,
    [ValidateSet("tcp", "udp")]
    [string]$Protocol = "tcp",
    [Parameter(Mandatory = $true)]
    [string]$Profile,
    [string]$Hops = "P1-L2:N4,P1-L2:N6,P1-L2:N2",
    [string]$LogDir = "logs\\relay_length_limit",
    [double]$ReadTimeoutSeconds = 10.0,
    [double]$WriteTimeoutSeconds = 20.0,
    [int]$Retries = 1,
    [int]$RelayCollisionRetries = 3,
    [int]$RelayCollisionDelayMs = 1000,
    [switch]$SkipWriteCheck
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$SmokeTestProject = Join-Path $RepoRoot "examples\\PlcComm.Toyopuc.SmokeTest"
$ResolvedLogDir = Join-Path $RepoRoot $LogDir

New-Item -ItemType Directory -Force -Path $ResolvedLogDir | Out-Null
Push-Location $RepoRoot

try {
    $commonArgs = @(
        "run",
        "--project", $SmokeTestProject,
        "--no-build",
        "--",
        "--host", $PlcHost,
        "--port", $Port,
        "--protocol", $Protocol,
        "--profile", $Profile,
        "--hops", $Hops,
        "--retries", $Retries,
        "--skip-status-read",
        "--skip-clock-read"
    )

    function Invoke-SmokeProbe {
        param(
            [Parameter(Mandatory = $true)]
            [string]$Device,
            [Parameter(Mandatory = $true)]
            [int]$Count,
            [Parameter(Mandatory = $true)]
            [double]$TimeoutSeconds,
            [Parameter(Mandatory = $true)]
            [string]$LogPath,
            [string]$WriteValue,
            [switch]$RestoreAfterWrite
        )

        $smokeArgs = @(
            "--device", $Device,
            "--count", ("0x{0:X}" -f $Count),
            "--timeout", $TimeoutSeconds.ToString([System.Globalization.CultureInfo]::InvariantCulture),
            "--log", $LogPath
        )

        if ($PSBoundParameters.ContainsKey("WriteValue")) {
            $smokeArgs += @(
                "--write-value", $WriteValue,
                "--write-pattern", "ramp"
            )

            if ($RestoreAfterWrite) {
                $smokeArgs += "--restore-after-write"
            }
        }

        $command = $commonArgs + $smokeArgs
        $argumentLine = ($command | ForEach-Object {
                if ($_ -match '[\s"]') {
                    '"' + $_.Replace('"', '\"') + '"'
                }
                else {
                    $_
                }
            }) -join " "
        $attemptLimit = [Math]::Max(1, $RelayCollisionRetries + 1)

        for ($attempt = 1; $attempt -le $attemptLimit; $attempt++) {
            if (Test-Path $LogPath) {
                Remove-Item $LogPath -Force
            }

            $stdoutPath = $LogPath + ".stdout.txt"
            $stderrPath = $LogPath + ".stderr.txt"

            if (Test-Path $stdoutPath) {
                Remove-Item $stdoutPath -Force
            }

            if (Test-Path $stderrPath) {
                Remove-Item $stderrPath -Force
            }

            $process = Start-Process `
                -FilePath "dotnet" `
                -ArgumentList $argumentLine `
                -NoNewWindow `
                -PassThru `
                -Wait `
                -RedirectStandardOutput $stdoutPath `
                -RedirectStandardError $stderrPath

            if ($process.ExitCode -eq 0) {
                Start-Sleep -Milliseconds 250
                return $true
            }

            $stderrText = if (Test-Path $stderrPath) {
                Get-Content $stderrPath -Raw
            }
            else {
                ""
            }

            if ($stderrText.Contains("error_code=0x73") -or $stderrText.Contains("Relay command collision")) {
                if ($attempt -lt $attemptLimit) {
                    Start-Sleep -Milliseconds $RelayCollisionDelayMs
                    continue
                }
            }

            Start-Sleep -Milliseconds 250
            return $false
        }

        return $false
    }

    function Find-MaxSuccessfulCount {
        param(
            [Parameter(Mandatory = $true)]
            [string]$Device,
            [Parameter(Mandatory = $true)]
            [int]$MaxCount,
            [Parameter(Mandatory = $true)]
            [double]$TimeoutSeconds,
            [Parameter(Mandatory = $true)]
            [string]$LogPrefix,
            [string]$WriteValue,
            [switch]$RestoreAfterWrite
        )

        $attempts = 0
        $lastLog = Join-Path $ResolvedLogDir ($LogPrefix + "_last.log")
        $invokeArgs = @{
            Device = $Device
            Count = $MaxCount
            TimeoutSeconds = $TimeoutSeconds
            LogPath = $lastLog
        }

        if ($PSBoundParameters.ContainsKey("WriteValue")) {
            $invokeArgs["WriteValue"] = $WriteValue
        }

        if ($RestoreAfterWrite) {
            $invokeArgs["RestoreAfterWrite"] = $true
        }

        $attempts++
        if (Invoke-SmokeProbe @invokeArgs) {
            return [pscustomobject]@{
                Device = $Device
                MaxCount = $MaxCount
                Attempts = $attempts
                LastLog = $lastLog
            }
        }

        $low = 1
        $high = $MaxCount - 1
        $best = 0

        while ($low -le $high) {
            $mid = [int][math]::Floor(($low + $high) / 2)
            $invokeArgs["Count"] = $mid
            $attempts++

            if (Invoke-SmokeProbe @invokeArgs) {
                $best = $mid
                $low = $mid + 1
            }
            else {
                $high = $mid - 1
            }
        }

        return [pscustomobject]@{
            Device = $Device
            MaxCount = $best
            Attempts = $attempts
            LastLog = $lastLog
        }
    }

    $probes = @(
        @{
            Label = "d_basic"
            Device = "P1-D0000"
            MaxCount = 0x3000
            WriteValue = "0x5100"
        },
        @{
            Label = "u_ext"
            Device = "U00000"
            MaxCount = 0x8000
            WriteValue = "0x5200"
        },
        @{
            Label = "u_pc10"
            Device = "U08000"
            MaxCount = 0x8000
            WriteValue = "0x5300"
        }
    )

    $results = New-Object System.Collections.Generic.List[object]

    foreach ($probe in $probes) {
        Write-Host ""
        Write-Host ("[read-limit] {0} {1} max-request=0x{2:X}" -f $probe.Label, $probe.Device, $probe.MaxCount)
        $readResult = Find-MaxSuccessfulCount `
            -Device $probe.Device `
            -MaxCount $probe.MaxCount `
            -TimeoutSeconds $ReadTimeoutSeconds `
            -LogPrefix ($probe.Label + "_read")

        Write-Host ("read-limit: max=0x{0:X} attempts={1}" -f $readResult.MaxCount, $readResult.Attempts)

        $writeMax = $null
        $writeAttempts = $null
        $writeLog = $null

        if (-not $SkipWriteCheck -and $readResult.MaxCount -gt 0) {
            Write-Host ("[write-limit] {0} {1} max-request=0x{2:X}" -f $probe.Label, $probe.Device, $readResult.MaxCount)
            $writeResult = Find-MaxSuccessfulCount `
                -Device $probe.Device `
                -MaxCount $readResult.MaxCount `
                -TimeoutSeconds $WriteTimeoutSeconds `
                -LogPrefix ($probe.Label + "_write") `
                -WriteValue $probe.WriteValue `
                -RestoreAfterWrite

            $writeMax = $writeResult.MaxCount
            $writeAttempts = $writeResult.Attempts
            $writeLog = $writeResult.LastLog
            Write-Host ("write-limit: max=0x{0:X} attempts={1}" -f $writeMax, $writeAttempts)
        }

        $results.Add([pscustomobject]@{
            Label = $probe.Label
            Device = $probe.Device
            MaxRequested = $probe.MaxCount
            ReadMaxCount = $readResult.MaxCount
            ReadAttempts = $readResult.Attempts
            ReadLog = $readResult.LastLog
            WriteMaxCount = $writeMax
            WriteAttempts = $writeAttempts
            WriteLog = $writeLog
        })
    }

    $csvPath = Join-Path $ResolvedLogDir "relay_length_limit_summary.csv"
    $results | Export-Csv -NoTypeInformation -Encoding UTF8 $csvPath

    Write-Host ""
    Write-Host ("summary-csv: {0}" -f $csvPath)
    $results | Format-Table -AutoSize
}
finally {
    Pop-Location
}
