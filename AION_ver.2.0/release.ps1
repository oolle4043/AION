param(
    [Parameter(Position = 0)]
    [ValidateSet("make", "clean")]
    [string]$Action = "make"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishDir = Join-Path $root "파괴아툴수집기"

function Stop-ReleaseProcesses {
    $processNames = @(
        "파괴아툴수집기",
        "파괴아툴수집엔진"
    )

    foreach ($processName in $processNames) {
        $processes = @(Get-Process -Name $processName -ErrorAction SilentlyContinue)
        foreach ($process in $processes) {
            Write-Host "실행 중인 프로세스 종료: $($process.ProcessName) (PID=$($process.Id))"
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
                Write-Host "삭제 완료: $PathValue"
                return
            }
            catch {
                if ($attempt -eq 5) {
                    throw "삭제 실패: $PathValue`n파일이 아직 사용 중일 수 있습니다. 실행 중인 앱, 결과 폴더, 탐색기 미리보기, 백신/OneDrive 동기화를 닫은 뒤 다시 실행하세요.`n원본 오류: $($_.Exception.Message)"
                }

                Write-Host "삭제 재시도 ${attempt}/5: $PathValue"
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
    Remove-IfExists (Join-Path $root "AION.Wpf\\bin")
    Remove-IfExists (Join-Path $root "AION.Wpf\\obj")
}

function Publish-All {
    & dotnet publish (Join-Path $root "AION.Collector.csproj") `
        -c Release `
        -r win-x64 `
        --self-contained true `
        /p:PublishSingleFile=true `
        -o $publishDir

    & dotnet publish (Join-Path $root "AION.Wpf\\AION.Wpf.csproj") `
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
            Write-Host "clean 완료"
        }
        "make" {
            Clean-Artifacts
            Publish-All
            Write-Host "make 완료: $publishDir"
        }
    }
}
finally {
    Pop-Location
}
