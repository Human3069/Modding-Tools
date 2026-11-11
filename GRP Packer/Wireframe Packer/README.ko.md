# StarCraft 1.16.1 Wireframe Packer

WireframePacker.exe를 더블 클릭하세요. Windows .NET Framework 4.x를 사용하며 Python 설치가 필요 없습니다.
현재 3개 폴더 일괄 변환을 누르면 상위 Stardrafts 폴더의 grp_wireframes, grp_tranwires, grp_grpwires를 읽습니다.
개별 RAL 파일이나 BMP 폴더도 선택할 수 있습니다. 출력은 기본적으로 output 폴더입니다.
RAL 첫 줄은 출력 GRP 이름이고, 이후 줄은 프레임 순서대로 BMP 이름입니다. 프레임 순서는 유닛 ID이므로 바꾸지 마세요.
RAL이 없으면 동일 접두어의 name000.bmp부터 번호 순서대로 읽으며 누락 번호를 거부합니다.
원본 입력 파일과 origins GRP는 수정하지 않습니다. 출력 파일 재생성 시 이전 파일은 .bak로 보관합니다.

## 변환 방식과 주의점

- 클래식 StarCraft GRP 헤더, 프레임 경계, 16비트 행 오프셋 및 RLE 투명/반복 명령을 기록합니다. 투명 인덱스는 0입니다.
- 8비트 indexed BMP는 RGB 색상으로 재매핑하지 않고 인덱스를 그대로 보존합니다. BI_RGB 및 RLE8 지원.
- RetroGRP가 각 BMP 행의 너비 밖에 기록한 여분 RLE 픽셀은 제외합니다.
- 24/32비트 BI_RGB BMP는 같은 폴더의 첫 8비트 BMP 팔레트와 정확히 일치하는 색으로만 변환합니다. 임의의 최근접 색 변환은 하지 않습니다.
- RGB 중복 색상은 상위 origins/unit/wirefram/*.grp의 동일 프레임/좌표 인덱스를 우선 복원합니다. 복원이 불가능한 새 픽셀은 일반형 와이어프레임 인덱스 208~211을 사용합니다. 다른 중복 색상은 오류로 중단합니다.
- 따라서 RGB 파일에서 새로 그린 저그형(216~219) 영역은 정확한 의미 복원이 보장되지 않습니다. 저그 체력 표시까지 정확히 지정하려면 처음부터 8비트 인덱스로 편집하세요. 팔레트 자체는 GRP에 저장되지 않습니다.
- 첫 BMP와 너비가 같고 높이만 더 큰 프레임은 추가 영역이 단색일 경우 아래쪽 추가 행을 제외합니다. 비단색 추가 영역이나 다른 크기는 오류로 중단합니다.
- 현재 grpwire025.bmp, grpwire030.bmp, grpwire092.bmp는 32x33입니다. 마지막 행(각각 흰색/흰색/검정색)을 제외하여 원본과 같은 32x32 GRP로 만들었습니다. 입력 BMP는 보존했습니다.
- 최대 2400프레임, BMP 가로/세로 1~255 지원. 일반 스프라이트의 전체 기능을 지원하는 범용 편집기가 아닌 와이어프레임 재조립 도구입니다.

## 생성 결과 및 검증

wirefram.grp: 228 프레임, 64x64
tranwire.grp: 106 프레임, 64x64
grpwire.grp: 131 프레임, 32x32

465 프레임 모두: 출력 GRP 재해독 후 입력에서 변환/크기 정규화한 인덱스와 전 픽셀 비교 통과.
Verify.cs/Verify.exe: 독립적인 System.Drawing BMP 디코더로 입력 BMP RGB와 출력 프레임을 비교하여 465 프레임 통과.
이 검증은 파일 구조와 픽셀 데이터 검증이며 StarCraft 1.16.1에서 실제 실행한 결과는 아닙니다.

## WinMPQ 삽입

파일 이름만이 아니라 다음 전체 내부 경로가 정확해야 합니다.

unit\wirefram\wirefram.grp
unit\wirefram\tranwire.grp
unit\wirefram\grpwire.grp

WinMPQ의 Add에서 output의 GRP 세 개를 선택하고, 경로 접두어를 묻는 창에 unit\wirefram\ 을 입력하세요.
추가 후 파일 목록에서 위 전체 경로를 확인하세요. 경로가 중복되면 Rename으로 수정하세요.
BrooDat.mpq를 직접 수정하기 전 백업하세요. Patch_rt.mpq나 모드 MPQ의 동일 경로가 우선 적용되면 BrooDat 쪽 변경이 보이지 않을 수 있습니다.
게임 실행 시 로드되는 최종 모드 MPQ에 위 경로로 넣는 방식을 권장합니다.

## 명령줄 / 빌드

WireframePacker.exe "..\grp_wireframes" "output"
WireframePacker.exe "..\grp_grpwires\grpwire.ral" "output"
WireframePacker.exe --verify "output\wirefram.grp"

build.cmd로 C# 소스에서 재빌드합니다. Verify.exe는 도구 폴더에서 실행합니다.

참조: https://github.com/poiuyqwert/PyMS/blob/master/PyMS/FileFormats/GRP.py
경로: https://staredit.net/topic/17094/
