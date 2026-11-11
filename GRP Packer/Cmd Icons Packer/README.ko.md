# StarCraft 1.16.1 Command Icons Packer

CmdIconsPacker.exe 실행 → 입력 RAL 확인 → 출력 폴더 선택 → GRP 만들기 + 검증.
기본 입력은 ../origins/unit/cmdbtns/cmdicons.ral이며, 기본 출력은 output/cmdicons.grp입니다.
C# WinForms / .NET Framework 4.x 프로그램입니다. Python이 필요하지 않습니다.
이 도구는 독립 실행되며 기존 WireframePacker.exe를 요구하지 않습니다.

## 입력과 출력

RAL 첫 줄은 출력 GRP 파일명, 이후 줄은 BMP 프레임 순서입니다. 현재 390개, 36x34입니다.
입력 이름이 있는 RAL을 기준으로 처리하므로 출력 이름은 cmdbtns.grp가 아닌 cmdicons.grp입니다.
RAL이 없는 폴더는 같은 접두어의 000부터 연속된 BMP를 번호순으로 읽습니다.
누락 BMP, RAL에 포함되지 않은 BMP, 중복 목록 및 서로 다른 캔버스 크기는 거부합니다.
원본 BMP/RAL/GRP는 수정하지 않습니다. 기존 출력이 있으면 직전 파일을 .bak에 보관합니다.

기존 cmdicons.grp를 읽어 클래식 RLE 압축 형식임을 확인했습니다. 출력도 같은 형식입니다.
이미지 경계를 계산하고 위치 오프셋을 유지하며, 팔레트 인덱스 0을 투명으로 기록합니다.
출력 압축 효율이 달라 원본과 파일 크기가 다르지만, 바이트 단위 동일성은 필요하지 않습니다.
헤더/프레임/행 오프셋 범위를 검사하고, 모든 출력 프레임을 재해독하여 입력 인덱스와 비교합니다.

## RGB BMP의 팔레트 복원

8비트 indexed BMP는 RGB 재매핑 없이 인덱스를 그대로 유지합니다. BI_RGB와 RLE8을 지원합니다.
RetroGRP가 RLE 행의 너비 밖에 기록한 여분 데이터는 제외합니다.
24/32비트 BI_RGB 파일은 같은 폴더의 첫 8비트 BMP 팔레트와 정확히 일치하는 색으로만 변환합니다.
32비트 BMP의 알파는 사용하지 않습니다. GRP 투명 여부는 변환된 인덱스 0으로 결정합니다.
팔레트 자체는 GRP에 저장되지 않으므로 게임이 적용하는 버튼 팔레트로 최종 확인해야 합니다.

현재 cmdicons091.bmp와 cmdicons092.bmp는 32비트 RGB입니다.
중복 RGB에 대해서 ../origins/unit/cmdbtns/cmdicons.grp의 같은 프레임/좌표 인덱스가 해당 색과 일치하면 원본 인덱스를 유지합니다.
일치하지 않으면 중복 후보 중 가장 낮은 인덱스를 선택하고 로그의 RGB fallback pixels에 누적합니다.
이번 결과는 798픽셀에 이 대체 규칙을 사용했습니다. 원래 인덱스의 완전 복원을 의미하지 않습니다.
RGB로 바꾸는 순간 같은 색의 여러 인덱스는 구별할 수 없게 됩니다. 게임에서 다른 색으로 보이면 해당 BMP를 정확한 팔레트의 8비트 indexed 이미지로 편집하세요.
선택한 입력 폴더가 달라도 원본 참조는 위 위치의 cmdicons.grp를 사용하므로, 다른 모드 소스를 사용할 때는 참조 파일도 확인하세요.
팔레트에 없는 RGB는 최근접 색상으로 바꾸지 않고 오류로 중단합니다.

## 검증 결과

390개 프레임 출력 재해독/인덱스 비교 통과.
독립적인 System.Drawing BMP 디코더로 모든 입력 RGB와 출력 인덱스를 BMP 팔레트로 표시한 RGB 비교 통과.
Verify.exe는 도구 폴더에서 실행합니다. 현재 입력 크기는 모두 36x34이며 잘라내거나 크기를 바꾸지 않았습니다.
StarCraft 1.16.1의 실제 게임 실행은 검증하지 않았습니다. 특히 위 RGB 변환 프레임은 게임 내 색상을 확인하세요.

## WinMPQ

output/cmdicons.grp를 모드 MPQ에 추가합니다.
전체 내부 경로: unit\cmdbtns\cmdicons.grp
경로 접두어를 물으면 unit\cmdbtns\ 를 입력합니다.

## 명령줄 및 소스

CmdIconsPacker.exe "..\origins\unit\cmdbtns\cmdicons.ral" "output"
CmdIconsPacker.exe --verify "output\cmdicons.grp"

CmdIconsPacker.cs: 프로그램 소스 / Verify.cs: 별도 RGB 검증 소스.
build.cmd로 재빌드합니다. 이전에 제공된 C# 코딩 규칙을 적용했습니다.
