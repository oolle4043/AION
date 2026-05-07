param(
    [Parameter(Position = 0)]
    [ValidateSet("make", "clean")]
    [string]$Action = "make"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$appName = -join ([char[]](
    0xD30C, 0xAD34, 0xC544, 0xD234, 0xC218, 0xC9D1, 0xAE30
))
$engineName = -join ([char[]](
    0xD30C, 0xAD34, 0xC544, 0xD234, 0xC218, 0xC9D1, 0xC5D4, 0xC9C4
))
$publishDir = Join-Path $root $appName

function Stop-ReleaseProcesses {
    $processNames = @(
        $appName,
        $engineName
    )

    foreach ($processName in $processNames) {
        $processes = @(Get-Process -Name $processName -ErrorAction SilentlyContinue)
        foreach ($process in $processes) {
            Write-Host "Stopping process: $($process.ProcessName) (PID=$($process.Id))"
            Stop-Process -Id $process.Id -Force
            [void]$process.WaitForExit(5000)
        }
    }
}

function Remove-IfExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue
    )

    if (Test-Path -LiteralPath $PathValue) {
        for ($attempt = 1; $attempt -le 5; $attempt++) {
            try {
                Remove-Item -LiteralPath $PathValue -Recurse -Force
                Write-Host "Removed: $PathValue"
                return
            }
            catch {
                if ($attempt -eq 5) {
                    throw "Failed to remove: $PathValue`nThe file may still be in use. Close the running app, output folder, Explorer preview, antivirus scan, or OneDrive sync, then try again.`nOriginal error: $($_.Exception.Message)"
                }

                Write-Host "Retry remove ${attempt}/5: $PathValue"
                Start-Sleep -Milliseconds 600
            }
        }
    }
}

function Clean-Artifacts {
    Stop-ReleaseProcesses
    Remove-IfExists (Join-Path $root "bin")
    Remove-IfExists (Join-Path $root "obj")
    Remove-IfExists (Join-Path $root "publish")
    Remove-IfExists $publishDir
    Remove-IfExists (Join-Path $root "publish-release")
    Remove-IfExists (Join-Path $root "AION.Wpf\bin")
    Remove-IfExists (Join-Path $root "AION.Wpf\obj")
}

function Publish-All {
    & dotnet publish (Join-Path $root "AION.Collector.csproj") `
        -c Release `
        -r win-x64 `
        --self-contained true `
        /p:PublishSingleFile=true `
        -o $publishDir

    & dotnet publish (Join-Path $root "AION.Wpf\AION.Wpf.csproj") `
        -c Release `
        -r win-x64 `
        --self-contained true `
        /p:PublishSingleFile=true `
        -o $publishDir
}

Push-Location $root
try {
    switch ($Action) {
        "clean" {
            Clean-Artifacts
            Write-Host "clean complete"
        }
        "make" {
            Clean-Artifacts
            Publish-All
            Write-Host "make complete: $publishDir"
        }
    }
}
finally {
    Pop-Location
}
