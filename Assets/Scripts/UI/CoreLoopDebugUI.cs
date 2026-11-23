using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TownDungeonLoopManager의 코어 루프(상점 구매, 창고↔가방 이동, 장비 장착/해제,
/// 던전 진입, 루팅, 전멸 페널티, 탈출 정산)를 검증하기 위한 디버그 OnGUI 모니터링 및 컨트롤러.
/// 단일 책임 원칙(SRP)에 따라 화면 출력 및 버튼 이벤트 바인딩만 담당하며,
/// 모든 게임 상태 변경 및 로직 처리는 TownDungeonLoopManager.Instance에 위임합니다.
/// </summary>
public class CoreLoopDebugUI : MonoBehaviour
{
    [Header("테스트용 아이템 에셋 매핑 (Assets/Resources/Items)")]
    [SerializeField] private ItemData item101_OranBerry; // 101_오랭열매
    [SerializeField] private ItemData item102_EscapeOrb; // 102_탈출구슬
    [SerializeField] private ItemData item201_IronSword; // 201_철검

    [Header("UI 표시 설정")]
    [SerializeField] private bool isVisible = true;

    // UI 스크롤 위치 상태
    private Vector2 leftScrollPos = Vector2.zero;
    private Vector2 rightScrollPos = Vector2.zero;
    private Vector2 storageScrollPos = Vector2.zero;
    private Vector2 bagScrollPos = Vector2.zero;
    private Vector2 equipScrollPos = Vector2.zero;

    // 최근 실행 피드백 메시지
    private string lastActionMessage = "준비 완료: 오른쪽 액션 버튼을 눌러 테스트를 시작하세요.";
    private Color lastActionColor = Color.white;

    // OnGUI 전용 캐싱 스타일
    private bool stylesInitialized = false;
    private GUIStyle panelBoxStyle;
    private GUIStyle titleStyle;
    private GUIStyle subTitleStyle;
    private GUIStyle statusHeaderStyle;
    private GUIStyle contentLabelStyle;
    private GUIStyle boldValueStyle;
    private GUIStyle actionButtonStyle;
    private GUIStyle dungeonActionButtonStyle;
    private GUIStyle listContainerStyle;
    private GUIStyle logMessageStyle;

    private void Awake()
    {
        InitItemReferences();
    }

    private void Start()
    {
        InitItemReferences();
    }

    /// <summary>
    /// 인스펙터에 매핑되지 않은 경우 Resources 폴더에서 자동 로드 보장
    /// </summary>
    public void InitItemReferences()
    {
        if (item101_OranBerry == null)
            item101_OranBerry = Resources.Load<ItemData>("Items/101_오랭열매");
        if (item102_EscapeOrb == null)
            item102_EscapeOrb = Resources.Load<ItemData>("Items/102_탈출구슬");
        if (item201_IronSword == null)
            item201_IronSword = Resources.Load<ItemData>("Items/201_철검");
    }

    private void Update()
    {
        bool f1Down = false;

#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f1Key.wasPressedThisFrame)
        {
            f1Down = true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
        if (Input.GetKeyDown(KeyCode.F1))
        {
            f1Down = true;
        }
#endif

        if (f1Down)
        {
            isVisible = !isVisible;
        }
    }

    private void InitStyles()
    {
        if (stylesInitialized && panelBoxStyle != null) return;

        panelBoxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(12, 12, 12, 12)
        };

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.95f, 0.95f, 1f) }
        };

        subTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.85f, 0.3f) }
        };

        statusHeaderStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.4f, 0.9f, 1f) }
        };

        contentLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            normal = { textColor = new Color(0.9f, 0.9f, 0.9f) },
            wordWrap = true
        };

        boldValueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        actionButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            fixedHeight = 30,
            alignment = TextAnchor.MiddleCenter
        };

        dungeonActionButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            fixedHeight = 30,
            alignment = TextAnchor.MiddleCenter
        };

        listContainerStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(6, 6, 6, 6)
        };

        logMessageStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 12,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = true,
            padding = new RectOffset(10, 10, 8, 8)
        };

        stylesInitialized = true;
    }

    private void OnGUI()
    {
        if (!isVisible) return;

        InitStyles();

        var loopManager = TownDungeonLoopManager.Instance;
        if (loopManager == null)
        {
            GUI.Box(new Rect(20, 20, 450, 100), "TownDungeonLoopManager 알림", panelBoxStyle);
            GUILayout.BeginArea(new Rect(30, 45, 430, 70));
            GUILayout.Label("[경고] TownDungeonLoopManager.Instance를 찾을 수 없습니다.", subTitleStyle);
            GUILayout.Label("씬의 GameManagers 오브젝트에 TownDungeonLoopManager가 부착되어 있는지 확인하세요.", contentLabelStyle);
            GUILayout.EndArea();
            return;
        }

        float totalW = Screen.width;
        float totalH = Screen.height;
        float margin = 16f;

        // 화면 분할: 좌측 모니터링 패널 (약 56%), 우측 액션 버튼 패널 (약 44%)
        float leftWidth = Mathf.Clamp(totalW * 0.56f, 480f, 720f);
        float rightWidth = totalW - leftWidth - margin * 3f;
        float panelHeight = totalH - margin * 2f;

        // 1. 좌측/중앙: 현재 상태 실시간 모니터링 패널
        Rect leftRect = new Rect(margin, margin, leftWidth, panelHeight);
        GUI.Box(leftRect, "", panelBoxStyle);
        GUILayout.BeginArea(new Rect(leftRect.x + 12, leftRect.y + 10, leftRect.width - 24, leftRect.height - 20));
        DrawMonitoringPanel(loopManager);
        GUILayout.EndArea();

        // 2. 우측: 단계별 테스트 액션 버튼 패널
        Rect rightRect = new Rect(margin * 2f + leftWidth, margin, rightWidth, panelHeight);
        GUI.Box(rightRect, "", panelBoxStyle);
        GUILayout.BeginArea(new Rect(rightRect.x + 12, rightRect.y + 10, rightRect.width - 24, rightRect.height - 20));
        DrawActionPanel(loopManager);
        GUILayout.EndArea();
    }

    /// <summary>
    /// 화면 좌측: 현재 상태 실시간 모니터링 패널 렌더링
    /// </summary>
    private void DrawMonitoringPanel(TownDungeonLoopManager loopManager)
    {
        GUILayout.Label("코어 루프 실시간 모니터링 패널 (F1: 토글)", titleStyle);
        GUILayout.Space(6);

        leftScrollPos = GUILayout.BeginScrollView(leftScrollPos, GUILayout.ExpandHeight(true));

        // 1) 현재 위치 상태 (Town / Dungeon)
        GUILayout.BeginHorizontal(listContainerStyle);
        GUILayout.Label("현재 위치 상태:", statusHeaderStyle, GUILayout.Width(110));
        if (loopManager.CurrentState == GameState.Town)
        {
            GUI.color = new Color(0.3f, 1f, 0.4f);
            GUILayout.Label("[ 마을 (Town) ]", boldValueStyle);
            GUI.color = Color.white;
        }
        else
        {
            GUI.color = new Color(1f, 0.4f, 0.3f);
            GUILayout.Label($"[ 던전 탐험 중 (Dungeon ID: {loopManager.CurrentDungeonId}) ]", boldValueStyle);
            GUI.color = Color.white;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        // 2) 골드 상태 비교: 마을 은행 골드 vs 지참 골드
        var accountData = loopManager.AccountData;
        var dungeonBag = loopManager.DungeonBag;

        GUILayout.BeginVertical(listContainerStyle);
        GUILayout.BeginHorizontal();
        GUILayout.Label("마을 은행 골드 (안전 보관):", statusHeaderStyle, GUILayout.Width(250));
        GUI.color = new Color(0.4f, 0.95f, 1f);
        GUILayout.Label($"{(accountData != null ? accountData.gold : 0):N0} G", boldValueStyle);
        GUI.color = Color.white;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("지참 골드 (위험 자산):", statusHeaderStyle, GUILayout.Width(250));
        GUI.color = new Color(1f, 0.65f, 0.2f);
        GUILayout.Label($"{(dungeonBag != null ? dungeonBag.acquiredGold : 0):N0} G", boldValueStyle);
        GUI.color = Color.white;
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        GUILayout.Space(6);

        // 3) 마을 창고 아이템 목록 (AccountData.storageItems)
        int storageCount = accountData != null && accountData.storageItems != null ? accountData.storageItems.Count : 0;
        GUILayout.Label($"마을 창고 목록 (AccountData.storageItems) - 총 {storageCount}슬롯", subTitleStyle);
        GUILayout.BeginVertical(listContainerStyle, GUILayout.Height(110));
        storageScrollPos = GUILayout.BeginScrollView(storageScrollPos);
        if (storageCount == 0)
        {
            GUILayout.Label("  (창고가 비어 있습니다)", contentLabelStyle);
        }
        else
        {
            for (int i = 0; i < accountData.storageItems.Count; i++)
            {
                var slot = accountData.storageItems[i];
                if (slot == null) continue;
                var itemData = loopManager.GetItemData(slot.itemId);
                string itemName = itemData != null ? itemData.itemName : $"아이템(ID:{slot.itemId})";
                string enhanceText = slot.enhanceLevel > 0 ? $" (+{slot.enhanceLevel})" : "";
                string typeText = itemData != null ? $"[{itemData.itemType}] " : "";

                GUILayout.Label($"  [{i}] {typeText}{itemName}{enhanceText}  x {slot.quantity}개", contentLabelStyle);
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.Space(6);

        // 4) 출정 가방 아이템 목록 (DungeonBag.bagItems)
        int maxBagSlots = loopManager.MaxBagSlots;
        int bagCount = dungeonBag != null && dungeonBag.bagItems != null ? dungeonBag.bagItems.Count : 0;

        GUILayout.BeginHorizontal();
        GUILayout.Label($"출정 가방 목록 (DungeonBag.bagItems)", subTitleStyle);
        GUILayout.FlexibleSpace();
        if (bagCount >= maxBagSlots)
        {
            GUI.color = new Color(1f, 0.4f, 0.4f);
            GUILayout.Label($"[슬롯 사용량: {bagCount} / {maxBagSlots} (만석)]", boldValueStyle);
            GUI.color = Color.white;
        }
        else
        {
            GUI.color = new Color(0.4f, 1f, 0.5f);
            GUILayout.Label($"[슬롯 사용량: {bagCount} / {maxBagSlots}]", boldValueStyle);
            GUI.color = Color.white;
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginVertical(listContainerStyle, GUILayout.Height(110));
        bagScrollPos = GUILayout.BeginScrollView(bagScrollPos);
        if (bagCount == 0)
        {
            GUILayout.Label("  (가방이 비어 있습니다)", contentLabelStyle);
        }
        else
        {
            for (int i = 0; i < dungeonBag.bagItems.Count; i++)
            {
                var slot = dungeonBag.bagItems[i];
                if (slot == null) continue;
                var itemData = loopManager.GetItemData(slot.itemId);
                string itemName = itemData != null ? itemData.itemName : $"아이템(ID:{slot.itemId})";
                string enhanceText = slot.enhanceLevel > 0 ? $" (+{slot.enhanceLevel})" : "";
                string typeText = itemData != null ? $"[{itemData.itemType}] " : "";

                GUILayout.Label($"  [{i}] {typeText}{itemName}{enhanceText}  x {slot.quantity}개", contentLabelStyle);
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.Space(6);

        // 5) 현재 장착 장비 목록 (EquippedItems)
        var equippedList = loopManager.EquippedItems;
        int equipCount = equippedList != null ? equippedList.Count : 0;
        GUILayout.Label($"현재 장착 장비 목록 (EquippedItems) - 총 {equipCount}개 (전멸 시 100% 보존)", subTitleStyle);
        GUILayout.BeginVertical(listContainerStyle, GUILayout.Height(80));
        equipScrollPos = GUILayout.BeginScrollView(equipScrollPos);
        if (equipCount == 0)
        {
            GUILayout.Label("  (장착된 장비가 없습니다)", contentLabelStyle);
        }
        else
        {
            for (int i = 0; i < equippedList.Count; i++)
            {
                var slot = equippedList[i];
                if (slot == null) continue;
                var itemData = loopManager.GetItemData(slot.itemId);
                string itemName = itemData != null ? itemData.itemName : $"장비(ID:{slot.itemId})";
                string enhanceText = slot.enhanceLevel > 0 ? $" (+{slot.enhanceLevel})" : "";

                GUI.color = new Color(0.4f, 0.95f, 1f);
                GUILayout.Label($"  [{i}] [장착중] {itemName}{enhanceText} (보호 적용)", boldValueStyle);
                GUI.color = Color.white;
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.Space(6);

        // 6) 최근 실행 피드백 로그 박스
        GUILayout.Label("최근 실행 결과 피드백 로그", subTitleStyle);
        GUI.color = lastActionColor;
        GUILayout.Box(lastActionMessage, logMessageStyle, GUILayout.Height(50));
        GUI.color = Color.white;

        GUILayout.EndScrollView();
    }

    /// <summary>
    /// 화면 우측: 단계별 테스트 액션 버튼 패널 렌더링
    /// </summary>
    private void DrawActionPanel(TownDungeonLoopManager loopManager)
    {
        GUILayout.Label("단계별 코어 루프 액션 패널", titleStyle);
        GUILayout.Space(6);

        rightScrollPos = GUILayout.BeginScrollView(rightScrollPos, GUILayout.ExpandHeight(true));

        // ==========================================
        // [디버그 편의 기능]
        // ==========================================
        GUI.color = new Color(1f, 0.85f, 0.2f);
        if (GUILayout.Button("[디버그] 마을 골드 +1,000 충전", actionButtonStyle))
        {
            loopManager.AddTownGold(1000);
            SetFeedback(true, $"[디버그] 마을 골드 +1,000G 충전 완료 (현재 골드: {loopManager.AccountData.gold:N0}G)");
        }
        GUI.color = Color.white;

        GUILayout.Space(6);

        // ==========================================
        // [마을 기능]
        // ==========================================
        GUILayout.Label("[ 마을 기능 (Town Loop) ]", subTitleStyle);
        GUILayout.Space(2);

        // 출정 골드 이동 버튼들 (마을 은행 입출금)
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("[마을 은행] +500G 입금 (지참→은행)", actionButtonStyle))
        {
            bool ok = loopManager.DepositGold(500);
            SetFeedback(ok, ok ? "[성공] 마을 은행에 500G 입금 완료. (지참→은행)" : "[실패] 지참 골드가 부족하거나 마을 상태가 아닙니다.");
        }
        if (GUILayout.Button("[마을 은행] +500G 출금 (은행→지참)", actionButtonStyle))
        {
            bool ok = loopManager.WithdrawGold(500);
            SetFeedback(ok, ok ? "[성공] 마을 은행에서 500G 출금 완료. (은행→지참)" : "[실패] 은행 골드가 부족하거나 마을 상태가 아닙니다.");
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        // 1. 상점 구매: 오랭열매(101) 1개 구매
        if (GUILayout.Button("1. 상점 구매: 오랭열매(101) 1개 (가방 우선)", actionButtonStyle))
        {
            var item = item101_OranBerry != null ? item101_OranBerry : loopManager.GetItemData(101);
            if (item != null)
            {
                bool ok = loopManager.BuyItem(item, 1);
                SetFeedback(ok, ok ? $"[성공] 오랭열매(101) 구매 완료. (잔여 골드: {loopManager.AccountData.gold:N0}G)" : $"[실패] 오랭열매(101) 구매 실패 (골드 부족 등)");
            }
            else
            {
                bool ok = loopManager.BuyItem(101, 1);
                SetFeedback(ok, ok ? "[성공] 오랭열매(101) 1개 구매 완료." : "[실패] 오랭열매(101) 마스터 데이터를 찾을 수 없습니다.");
            }
        }

        // 2. 상점 구매: 탈출구슬(102) 1개 구매
        if (GUILayout.Button("2. 상점 구매: 탈출구슬(102) 1개 (가방 우선)", actionButtonStyle))
        {
            var item = item102_EscapeOrb != null ? item102_EscapeOrb : loopManager.GetItemData(102);
            if (item != null)
            {
                bool ok = loopManager.BuyItem(item, 1);
                SetFeedback(ok, ok ? $"[성공] 탈출구슬(102) 구매 완료. (잔여 골드: {loopManager.AccountData.gold:N0}G)" : $"[실패] 탈출구슬(102) 구매 실패 (골드 부족 등)");
            }
            else
            {
                bool ok = loopManager.BuyItem(102, 1);
                SetFeedback(ok, ok ? "[성공] 탈출구슬(102) 1개 구매 완료." : "[실패] 탈출구슬(102) 마스터 데이터를 찾을 수 없습니다.");
            }
        }

        // 3. 상점 구매: 철검(201) 1개 구매
        if (GUILayout.Button("3. 상점 구매: 철검(201) 1개 (가방 우선)", actionButtonStyle))
        {
            var item = item201_IronSword != null ? item201_IronSword : loopManager.GetItemData(201);
            if (item != null)
            {
                bool ok = loopManager.BuyItem(item, 1);
                SetFeedback(ok, ok ? $"[성공] 철검(201) 구매 완료. (잔여 골드: {loopManager.AccountData.gold:N0}G)" : $"[실패] 철검(201) 구매 실패 (골드 부족 등)");
            }
            else
            {
                bool ok = loopManager.BuyItem(201, 1);
                SetFeedback(ok, ok ? "[성공] 철검(201) 1개 구매 완료." : "[실패] 철검(201) 마스터 데이터를 찾을 수 없습니다.");
            }
        }

        GUILayout.Space(4);

        // 4. 장비 장착 (가방에 있는 첫 번째 Equipment 아이템 장착)
        if (GUILayout.Button("4. 장비 장착 (가방 첫 번째 Equipment 장착)", actionButtonStyle))
        {
            bool ok = loopManager.EquipFirstEquipmentFromBag();
            SetFeedback(ok, ok ? "[성공] 가방의 첫 번째 장비 아이템을 장착했습니다." : "[실패] 가방에 장착 가능한 Equipment 아이템이 없습니다.");
        }

        // 5. 장비 해제 (0번 장착 슬롯 장비 가방으로 해제)
        if (GUILayout.Button("5. 장비 해제 (0번 장착 슬롯 가방으로 해제)", actionButtonStyle))
        {
            bool ok = loopManager.UnequipItemToBag(0);
            SetFeedback(ok, ok ? "[성공] 0번 슬롯 장비를 해제하여 가방으로 반환했습니다." : "[실패] 해제할 0번 장착 슬롯 장비가 없습니다.");
        }

        // 6. 가방 → 창고 이동 (0번 슬롯 아이템 1개 이동)
        if (GUILayout.Button("6. 가방 → 창고 이동 (0번 슬롯 1개)", actionButtonStyle))
        {
            bool ok = loopManager.MoveBagToStorage(0, 1);
            SetFeedback(ok, ok ? "[성공] 가방 0번 슬롯 아이템 1개를 창고로 이동했습니다." : "[실패] 가방 0번 슬롯 아이템 이동 실패 (가방이 비어있음)");
        }

        // 7. 창고 → 가방 이동 (0번 슬롯 아이템 1개 이동)
        if (GUILayout.Button("7. 창고 → 가방 이동 (0번 슬롯 1개)", actionButtonStyle))
        {
            bool ok = loopManager.MoveStorageToBag(0, 1);
            SetFeedback(ok, ok ? "[성공] 창고 0번 슬롯 아이템 1개를 가방으로 이동했습니다." : "[실패] 창고 0번 슬롯 아이템 이동 실패 (창고가 비어있음)");
        }

        GUILayout.Space(14);

        // ==========================================
        // [던전 루프 기능]
        // ==========================================
        GUILayout.Label("[ 던전 루프 기능 (Dungeon Loop) ]", subTitleStyle);
        GUILayout.Space(2);

        // 8. 던전 진입 (EnterDungeon(1))
        GUI.color = new Color(0.9f, 0.9f, 1f);
        if (GUILayout.Button("8. 던전 진입 (EnterDungeon(1))", dungeonActionButtonStyle))
        {
            loopManager.EnterDungeon(1);
            SetFeedback(true, "[성공] 던전(ID: 1)에 진입했습니다. (Dungeon 상태로 전환)");
        }
        GUI.color = Color.white;

        // 9. 던전 루팅: 오랭열매(101) 2개 + 200 골드 획득
        if (GUILayout.Button("9. 던전 루팅: 오랭열매(101) 2개 + 200 골드 획득", dungeonActionButtonStyle))
        {
            if (loopManager.CurrentState != GameState.Dungeon)
            {
                SetFeedback(false, "[경고] 던전 상태(GameState.Dungeon)일 때만 루팅할 수 있습니다. 8번 '던전 진입'을 먼저 실행하세요.");
            }
            else
            {
                loopManager.AddLootInDungeon(101, 2);
                loopManager.AddGoldInDungeon(200);
                SetFeedback(true, "[성공] 전리품 획득: 오랭열매(101) 2개 + 임시 골드 200G 획득");
            }
        }

        // 10. 던전 루팅: 탈출구슬(102) 1개 + 150 골드 획득
        if (GUILayout.Button("10. 던전 루팅: 탈출구슬(102) 1개 + 150 골드 획득", dungeonActionButtonStyle))
        {
            if (loopManager.CurrentState != GameState.Dungeon)
            {
                SetFeedback(false, "[경고] 던전 상태(GameState.Dungeon)일 때만 루팅할 수 있습니다. 8번 '던전 진입'을 먼저 실행하세요.");
            }
            else
            {
                loopManager.AddLootInDungeon(102, 1);
                loopManager.AddGoldInDungeon(150);
                SetFeedback(true, "[성공] 전리품 획득: 탈출구슬(102) 1개 + 임시 골드 150G 획득");
            }
        }

        GUILayout.Space(4);

        // 11. 던전 전멸 테스트 (OnPlayerDefeated - 장착 100% 보호 & 비장착 50% 랜덤 소실 & 골드 10% 차감 확인)
        GUI.color = new Color(1f, 0.75f, 0.75f);
        if (GUILayout.Button("11. 던전 전멸 테스트 (OnPlayerDefeated)", dungeonActionButtonStyle))
        {
            loopManager.OnPlayerDefeated();
            SetFeedback(true, "[완료] 플레이어 전멸 처리 실행! (골드 10% 차감, 장착 장비 100% 보존, 가방 50% 소실 - 콘솔 로그 확인)");
        }
        GUI.color = Color.white;

        // 12. 던전 탈출 성공 (가방/골드 보존 복귀)
        GUI.color = new Color(0.75f, 1f, 0.75f);
        if (GUILayout.Button("12. 던전 탈출 성공 (가방/골드 보존 복귀)", dungeonActionButtonStyle))
        {
            loopManager.OnDungeonEscaped(1);
            SetFeedback(true, "[완료] 던전 탈출 성공! (획득한 아이템 및 골드가 가방에 그대로 보존된 채 마을 복귀)");
        }
        GUI.color = Color.white;

        GUILayout.EndScrollView();
    }

    private void SetFeedback(bool success, string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        lastActionMessage = $"[{timestamp}] {message}";
        lastActionColor = success ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.5f, 0.5f);
    }
}
