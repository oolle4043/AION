# AION

# 파괴 아툴 수집기
AION2 나니아서버 파괴 레기온 전투력, 아툴 측정기

## 디렉터리 구조

```text
.
├─ src/
│  └─ Collector.Console/
│     ├─ Program.cs
│     ├─ Program.Commands.cs
│     ├─ Program.Data.cs
│     ├─ Program.ListAndExcel.cs
│     └─ Program.Web.cs
├─ AION.WinForms/
│  ├─ Program.cs
│  └─ Forms/
│     └─ MainForm/
│        ├─ MainForm.cs
│        ├─ MainForm.Process.cs
│        ├─ MainForm.Settings.cs
│        ├─ MainForm.TargetDialog.cs
│        └─ MainForm.IO.cs
├─ AION.Collector.csproj
├─ AION.WinForms/AION.WinForms.csproj
└─ AION.sln
```

## 실행/빌드

```powershell
# 콘솔
$env:DOTNET_CLI_HOME = (Resolve-Path .dotnet_home).Path
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
dotnet build .\AION.Collector.csproj -c Debug -p:UseSharedCompilation=false

```
## 배포
.\release.ps1 make
.\release.ps1 clean

## 버전
- `AION_ver.1.0`
  - 기존 WinForms 기반 버전
  - WPF 전환 전 마지막 구조와 코드를 보관

- `AION_ver.2.0`
  - WPF 전환 버전
  - 기존 기능을 유지하면서 WPF 실행 프로젝트를 추가한 최신 구조

- `AION_Party.1.0`
  - 루드라 공대 파티 매칭 프로그램
