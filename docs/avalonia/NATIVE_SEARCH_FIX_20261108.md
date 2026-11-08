# 일반 실행 창의 검색 후 화면 멈춤 수정 — 2026-11-08

## 재현과 이전 검증의 한계

사용자가 재차 지적한 뒤 진단 옵션 없이 루트 Stock.Avalonia.exe를 실행했다. 실제 창의 검색 TextBox에 종목명을 입력하고 마우스로 검색 버튼을 눌렀다. 시세·제목·버튼 상태는 UIAutomation에서 갱신됐지만 **실제 화면은 이전 로딩 프레임에 머물렀다**. 네트워크 조회가 끝나도 로딩처럼 보이는 증상을 Windows에서 재현했다.

이전 --verify-public 검사는 명령을 ViewModel에서 직접 실행하고 RenderTargetBitmap으로 시각 트리를 별도 렌더링했다. 따라서 데이터/차트 객체의 성공은 검증했지만 일반 창의 화면 반영 실패를 놓쳤다. 그 결과만으로 사용자에게 정상이라고 보고한 판단을 정정한다. UIAutomation의 값이 정상이라는 것 역시 실제 화면 표시의 증거로 충분하지 않았다.

## 수정

`Stock.Avalonia/Program.cs`의 Win32PlatformOptions에 다음을 적용했다.

```csharp
CompositionMode = [Win32CompositionMode.RedirectionSurface],
ShouldRenderOnUIThread = true
```

별도 렌더 스레드에서 실제 창 프레임 갱신이 멈추는 경로를 피해 Windows UI dispatcher에서 표시하도록 한다. 공개 조회·전략 계산·공통 Core·사용자 저장 자료는 바꾸지 않는다. GPU/소프트웨어 선택은 Avalonia 기본값을 유지한다. Mac 플랫폼 설정은 변경하지 않는다.

관측된 비교: 기본 설정 실패, RedirectionSurface 단독 실패, Software+RedirectionSurface 단독 실패, UI 스레드 표시를 함께 사용하면 성공했다. 따라서 단순 네트워크 타임아웃 또는 GPU 가속 하나의 문제로 단정하지 않는다. 스레드 보고는 UI 메시지 루프가 살아 있는 상태임을 확인했으며 개인정보를 포함하는 메모리 덤프는 수집하지 않았다.

설치된 Avalonia12.1.5 Win32 XML/API와 [공식 Windows 옵션 문서](https://docs.avaloniaui.net/docs/platform-specific-guides/windows)를 기준으로 구성했다. ShouldRenderOnUIThread는 RedirectionSurface에서 적용되는 옵션이다. 실제 Windows에서 성공을 확인했으며 Mac 실기 검증을 대신하지 않는다.

## 추가 회귀 검사

`scripts/verify-native-search.ps1`는 Windows에서 **인수 없는 일반 앱**을 실행한다.

- 실제 Windows 키보드 이벤트로 삼성전자·SK하이닉스를 입력한다.
- 검색 버튼 실제 마우스 클릭과 TextBox의 Enter 경로를 각각 실행한다.
- 화면에서 직접 캡처한다. RenderTargetBitmap/Window.Render를 호출하지 않는다.
- 제목 종목코드/검색 버튼 복구와 함께 차트 영역의 빨강·파랑 봉 픽셀을 검사한다. 로딩 진행 막대만으로는 통과할 수 없다.
- 종목 전환·반복 검색과 앱 정상 종료 후 재실행을 포함한다.
- 자신이 시작한 프로세스만 닫으며 계좌 창/키/주문은 조작하지 않는다. 일반 실행이므로 독립 Avalonia 앱의 기본 저장 경로를 사용한다.

수정 전 루트 C302판: 내부 결과는 삼성전자005930이지만 실제 차트 빨강0/파랑15표본으로 실패했다. `artifacts/native-search-before/result.json`과 화면 PNG를 보존했다.

수정 빌드:4회 모두 실제 봉 표시 성공. 삼성전자 빨강590/파랑444, SK하이닉스 빨강422/파랑391표본. 검색/대기/캡처 포함 약1.2~2.0초이며 보장 시간이 아니다. `artifacts/native-search-after-build/result.json`.

최종 루트 EXE는107,277,111bytes, SHA256 **F86BCC3FF384669B3C85A8A5B698DEAC9C9BA03303B5309A8D463DF9FDD6F09A**다. 루트 새 EXE에서도4회 모두 실제 화면 봉 표시 성공(약1.3~2.0초), `artifacts/native-search-final/result.json`에 기록했다. 이전 C302판은 `artifacts/before-native-presentation-fix-20261108/`에 보존했다.

전체 Release 빌드0경고0오류/Core192/UI116/디자인16 성공(`native-search-final-regression.log`). 새 배포 EXE의 시작 smoke도Exit0. Mac 양 아키텍처는 교차 빌드했지만 실기 검증은 아니다. 원본 WPF와 공통 Core에는 변경이 없다. 기존 MODE_AUDIT_20261108의 미재현 결론은 이 보고서로 갱신한다.
