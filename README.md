dotnet publish .\test.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o .\파괴아툴수집기
dotnet publish .\test.WinForms\test.WinForms.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o .\파괴아툴수집기

https://docs.google.com/spreadsheets/d/1apppLkRRVOsc1mTn_86CAdf8P4P5UOIf2X6YLRQ43P0/edit?gid=1035406315#gid=1035406315


추가 개발 계획
1. A열을 빼고 B열에 는 1~n 인원수 넣기 (완료)

2. 직업, 이름 가나다순 정렬 (완료)

3. CSV -> 엑셀 파일 형식 수정 (완료)

4. 각 그룹마다 색 넣기 (완료)

5. 레벨 상승량, 아툴 상승량 색 추가 (완료)

6. 아툴, 투력 상승량이 마이너스면 빨간색 표시 (완료)

7. 설정 추가 (완료)
    - DB에 있는 서버, 한번에 검색하는 회수 설정
        -> 설정 누르면 DB에서 기존의 값 가져오게 하기.
        
8. WinForm UI 수정 (완료)
    - 버튼 위치 (실행중) 변경, 로그에 시작 프로그램 설명 추가
    - 추가/삭제버튼 누르면 그냥 바로 닉네임 입력하는 칸이 나오고 그 밑에 추가, 삭제, 취소버튼이 있게 하고 추가, 삭제를 누르면 해당 창이 유지되고 기존에 입력한 이름은 다시 초기화돼서 빈칸으로 나오게

9. 추가/삭제창 UI 수정
    - 닉네임 입력 텍스트 밑체 텍스트 창 밑으로 조금 내리고 둥글게 변경, 추가 삭제 취소 버튼 색 삭제