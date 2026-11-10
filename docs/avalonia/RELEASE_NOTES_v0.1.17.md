# Stock v0.1.17 · Windows / Apple Silicon Mac

전략 설정을 한 화면으로 정리하고 급락 판단 전략의 사용 여부·개별 봉 기준·JSON 불러오기를 개선했습니다. Windows와 Mac은 동일한 Avalonia·공통 Core 소스를 사용합니다.

## 다운로드

- [Windows x64 · Stock.exe](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.17/Stock.exe): 실행 파일 하나이며 .NET을 포함합니다.
- [Apple Silicon Mac · DMG](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.17/Stock-AppleSilicon-v0.1.17-r1.dmg) / [ZIP](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.17/Stock-AppleSilicon-v0.1.17-r1.zip): M1 이후·macOS14 이상, Stock.app을 응용 프로그램으로 옮겨 실행합니다. Mac 자산은 Windows 게시 후 Mac 검사가 모두 통과하면 추가됩니다.
- 각 실행 파일·압축 파일의 `.sha256`은 다운로드 무결성 확인용입니다. 프로그램에 .NET 별도 설치가 필요하지 않습니다.

## 변경 사항

- 전략 설정 순서를 `백테스트 전용 → 급락 판단 전략 → 공통 전략 조건`으로 정리했습니다. 별도 전략 D 설정 버튼·탭, 시점별 검증 자료 상위 영역과 수동 종목상태·위험·시장세션 JSON 입력창을 제거했습니다.
- 급락 판단 전략의 사용 체크를 접힌 제목 옆에 배치했습니다. 사용 OFF여도 하위 그룹을 펼칠 수 있으며 입력만 비활성화됩니다. OFF는 기존 전략, ON은 D 전체 진입·청산 규칙을 선택합니다.
- 지표 기간에 분·시간·일봉·주봉·월봉 단위를 붙이고 온라인 판단 자료를 차트 표시 주기와 분리했습니다. 이동평균은 일봉·주봉·월봉입니다. 오프라인 백테스트·스크리닝은 일봉 기준임을 표시합니다.
- 검은색 실제 일봉 MA240과 D 매수에 사용된 저점의 보라색 ‘급락 판단 지점’ 선을 표시합니다. 짧은 결과에서도 준비 자료를 사용하며, 부족한 자료로 선을 만들지 않습니다.
- 불필요한 숫자 소수 자리 표시를 제거하고 실제 소수 수수료·비율의 정밀도는 유지했습니다.
- JSON에서 가져온 5분·3시간 등 지표 봉 크기가 편집 과정에서1로 바뀌는 문제를 수정했습니다. 항목명 오타·중첩 중복 키·잘못된 단위 및 OFF 상태의 잘못된 D 설정을 저장 전에 거부합니다. 기존 숫자 enum 형식을 유지하며 영문 enum 이름 가져오기도 지원합니다.
- [현재 전략 상세 설명서](https://github.com/BaDook2/C-_STOCK/blob/v0.1.17/docs/strategy/현재_전략_상세설명_및_JSON_가이드_2026-11-10.md)에 설정 경로·기본값·계산식·진입/청산과 기존/D ON 전체 JSON을 함께 수록했습니다. 예제는 개인 저장값이 아닌 코드 기본값입니다.
- Rider에서 소스 수정 후 로컬 Mac .app을 만드는 공유 실행 설정을 추가했습니다.

## 검증과 지원 범위

로컬 Mac에서 Release 빌드0경고·0오류, Core192개·Avalonia629개·디자인18개, 그중 JSON 관련303개를 확인했습니다. 배포 CI는 추가로 Windows WPF 회귀와 배포 EXE 시작, Mac 원본 앱·ZIP·DMG 시작·공개 차트·격리 더미 Keychain 검사를 통과한 뒤 각 자산을 공개하며, 공개 파일 재다운로드의 해시·실행 검사를 수행합니다.

Mac은 ad-hoc 서명 무결성을 검사하며 Apple Developer ID 서명·Apple 공증은 포함하지 않습니다. 최초 실행 안내는 [Mac 설치 문서](https://github.com/BaDook2/C-_STOCK/blob/v0.1.17/docs/avalonia/MACOS_APPLE_SILICON.md)를 참고하세요. 사용자 Mac의 물리 입력·권한 승인과 실제 증권사 API는 자동 검사와 별개입니다.

급락 판단 전략 D의 실계좌 자동 주문은 지원하지 않습니다. D 당일 운용을 일봉만으로 재현할 수 없으며 자료·시각 조건에 따라 거래가0건일 수 있습니다. 실제 계좌·API 키·개인 DB를 배포 파일에 포함하지 않고 실제 돈 주문을 검사하지 않습니다.
