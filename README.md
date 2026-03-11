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
├─ test.WinForms/
│  ├─ Program.cs
│  └─ Forms/
│     └─ MainForm/
│        ├─ MainForm.cs
│        ├─ MainForm.Process.cs
│        ├─ MainForm.Settings.cs
│        ├─ MainForm.TargetDialog.cs
│        └─ MainForm.IO.cs
├─ test.csproj
├─ test.WinForms/test.WinForms.csproj
└─ test.sln
```

## 실행/빌드

```powershell
# 콘솔
$env:DOTNET_CLI_HOME = (Resolve-Path .dotnet_home).Path
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
dotnet build .\test.csproj -c Debug -p:UseSharedCompilation=false

```
## 배포
dotnet publish .\test.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o .\파괴아툴수집기
dotnet publish .\test.WinForms\test.WinForms.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o .\파괴아툴수집기

## 참고 자료
https://docs.google.com/spreadsheets/d/1apppLkRRVOsc1mTn_86CAdf8P4P5UOIf2X6YLRQ43P0/edit?gid=1035406315#gid=1035406315
```


# WinForms
$env:DOTNET_CLI_HOME = (Resolve-Path .dotnet_home).Path
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
dotnet build .\test.WinForms\test.WinForms.csproj -c Debug -p:UseSharedCompilation=false
```

## 참고

- 코드 로직은 유지하고 파일만 기능별로 분리했습니다.
- 솔루션(`test.sln`) 단위 빌드는 현재 환경에서 로그 없이 실패 코드(1)를 반환하는 현상이 있었고,
  각 프로젝트 개별 빌드는 정상 완료되었습니다.
제 취소 버튼 색 삭제


