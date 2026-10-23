# 선물용 캐릭터 폴더

캐릭터마다 폴더 하나씩 씁니다. 지금은 `a`, `b` 두 개가 준비되어 있어요.

```
Gift/
├─ gift_builds.json   ← 이름, 하트 색, 표정 등 설정
├─ a/
│  ├─ character.vrm   ← 캐릭터 A 모델 (Blender에서 VRM으로 내보낸 파일)
│  └─ icon.png        ← 프로그램 아이콘 (정사각형 PNG, 256×256 이상 추천)
└─ b/
   ├─ character.vrm
   └─ icon.png
```

`.vrm` 파일은 `.gitignore`에 들어 있어서 GitHub에 올라가지 않아요. 저장소가 공개라서 모델이 그대로 노출되지 않게 하려는 거예요.

## gift_builds.json 에서 바꿀 것

| 항목 | 뜻 |
|---|---|
| `productName` | 창 제목, 설정 저장 위치에 쓰이는 앱 이름 |
| `exeName` | 실행 파일 이름 (영어 권장) |
| `profile.characterName` | 캐릭터 이름 |
| `profile.heartColors` | 클릭할 때 나오는 하트 색 (여러 개면 섞여서 나와요) |
| `profile.heartCountMin` / `heartCountMax` | 한 번 클릭에 나오는 하트 개수 |
| `profile.heartSize` | 하트 크기 배율 |
| `profile.tintHearts` | `true`면 heartColors로 물들임, `false`면 heart.png 원래 색 그대로 |
| `profile.clickExpression` | 클릭했을 때 표정: `happy` `angry` `sad` `relaxed` `surprised` `none` |
| `profile.scruffExpression` | 목덜미 잡혔을 때 표정 (위와 같은 값) |
| `profile.swingStrength` | 대롱대롱 흔들림 세기 (0 = 안 흔들림) |
| `profile.legKick` | 발버둥 세기 (0 = 안 함) |
| `profile.useGrabCursor` | 잡을 때 손 모양 커서 사용 여부 (기본: 안 씀, 평소 마우스 커서 그대로) |

표정은 VRM 모델에 해당 표정(BlendShape/Expression)이 만들어져 있어야 보여요.

## 빌드하기

1. Unity Hub로 이 프로젝트를 엽니다 (Unity **6000.2.6f2**).
2. 위쪽 메뉴 **YUYI MINI ▸ Gift Builder**를 엽니다.
3. 캐릭터별 **빌드하기** 또는 **전부 빌드하기**를 누릅니다.
4. 결과물: `Builds/a/`, `Builds/b/` 폴더와 `Builds/a.zip`, `Builds/b.zip`

빌드할 때 원본 기본 아바타(Zome)는 자동으로 빠지고, 확인 결과가 `BUILD_CHECK.txt`에 적혀요.
기본 아바타가 남아 있으면 zip을 만들지 않고 경고를 띄웁니다.

## 이미지 바꾸기

`Assets/StreamingAssets/YuyiMini/` 안의 파일을 같은 이름으로 바꾸면 돼요.

- `cursor_grab.png` — 잡을 때 커서 (64×64, 집는 지점이 왼쪽에서 28, 위에서 46 픽셀)
- `heart.png` — 하트 이미지. 배경이 투명한 PNG, 정사각형 권장 (예: 256×256)
  - 흰색/회색으로 그리면 `heartColors` 색으로 물들어요 (`tintHearts: true`)
  - 직접 색칠한 하트를 그대로 쓰려면 `tintHearts`를 `false`로 바꾸세요
