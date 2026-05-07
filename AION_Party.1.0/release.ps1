param(
    [Parameter(Position = 0)]
    [ValidateSet("make", "clean")]
    [string]$Action = "make"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishDir = Join-Path $root "publish"

function Stop-ReleaseProcesses {
    $processes = @(Get-Process -Name "AION.Party" -ErrorAction SilentlyContinue)
    foreach ($process in $processes) {
        Write-Host "Stopping process: $($process.ProcessName) (PID=$($process.Id))"
        Stop-Process -Id $process.Id -Force
        [void]$process.WaitForExit(5000)
    }
}

function Remove-IfExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue
    )

    if (Test-Path -LiteralPath $PathValue) {
        Remove-Item -LiteralPath $PathValue -Recurse -Force
        Write-Host "Removed: $PathValue"
    }
}

Push-Location $root
try {
    Stop-ReleaseProcesses
    Remove-IfExists (Join-Path $root "bin")
    Remove-IfExists (Join-Path $root "obj")
    Remove-IfExists $publishDir

    if ($Action -eq "make") {
        & dotnet publish (Join-Path $root "AION.Party.csproj") `
            -c Release `
            -r win-x64 `
            --self-contained true `
            /p:PublishSingleFile=true `
            -o $publishDir

        Write-Host "make complete: $publishDir"
    }
    else {
        Write-Host "clean complete"
    }
}
finally {
    Pop-Location
}
