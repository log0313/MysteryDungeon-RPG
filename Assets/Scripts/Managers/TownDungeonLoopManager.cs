using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 마을-던전 루프, 게임 상태 및 세션 데이터를 총괄하는 싱글톤 매니저
/// </summary>
public class TownDungeonLoopManager : MonoBehaviour
{
    public static TownDungeonLoopManager Instance { get; private set; }

    [Header("현재 게임 상태")]
    [SerializeField] private GameState currentState = GameState.Town;
    public GameState CurrentState => currentState;

    [Header("현재 탐험 중인 던전")]
    [SerializeField] private int currentDungeonId = -1;
    public int CurrentDungeonId => currentDungeonId;

    [Header("세션 데이터")]
    [SerializeField] private AccountData accountData = new AccountData();
    [SerializeField] private DungeonBag dungeonBag = new DungeonBag();

    [Header("가방 설정")]
    [SerializeField] private int maxBagSlots = 20;
    public int MaxBagSlots => maxBagSlots;

    public AccountData AccountData => accountData;
    public DungeonBag DungeonBag => dungeonBag;
    public List<InventorySlot> EquippedItems => dungeonBag.equippedItems;

    // 상태 및 데이터 변화 알림 이벤트
    public event Action<GameState> OnStateChanged;
    public event Action<AccountData> OnAccountDataChanged;
    public event Action<DungeonBag> OnDungeonBagChanged;
    public event Action<List<InventorySlot>> OnEquippedItemsChanged;

    // 아이템 마스터 데이터 캐시
    private readonly Dictionary<int, ItemData> itemDatabase = new Dictionary<int, ItemData>();

    public TownDungeonLoopManager()
    {
        SyncEquippedItems();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SyncEquippedItems();
        LoadItemDatabase();
    }

    /// <summary>
    /// AccountData와 DungeonBag의 장착 아이템 리스트 참조를 동기화하여 일관성 유지
    /// </summary>
    private void SyncEquippedItems()
    {
        if (accountData == null)
            accountData = new AccountData();
        if (dungeonBag == null)
            dungeonBag = new DungeonBag();

        if (accountData.equippedItems == null)
            accountData.equippedItems = new List<InventorySlot>();

        dungeonBag.equippedItems = accountData.equippedItems;
    }

    /// <summary>
    /// Resources/Items 경로의 ItemData ScriptableObject 에셋을 로드하여 캐시
    /// </summary>
    public void LoadItemDatabase()
    {
        itemDatabase.Clear();
        var items = Resources.LoadAll<ItemData>("Items");
        foreach (var item in items)
        {
            if (item != null && !itemDatabase.ContainsKey(item.itemId))
            {
                itemDatabase.Add(item.itemId, item);
            }
        }
        Debug.Log($"[TownDungeonLoopManager] 아이템 데이터베이스 로드 완료: 총 {itemDatabase.Count}개 등록됨.");
    }

    /// <summary>
    /// ID로 아이템 마스터 데이터 조회
    /// </summary>
    public ItemData GetItemData(int itemId)
    {
        if (itemDatabase.Count == 0)
            LoadItemDatabase();

        itemDatabase.TryGetValue(itemId, out var data);
        return data;
    }

    /// <summary>
    /// 던전 진입: 던전 상태(GameState.Dungeon)로 전환 (마을에서 챙긴 가방 아이템 및 장착 장비 유지)
    /// </summary>
    public void EnterDungeon(int dungeonId)
    {
        SyncEquippedItems();
        currentDungeonId = dungeonId;
        currentState = GameState.Dungeon;

        Debug.Log($"[TownDungeonLoopManager] 던전(ID: {dungeonId})에 진입했습니다. (가방 아이템 {dungeonBag.bagItems.Count}개, 장착 장비 {dungeonBag.equippedItems.Count}개 지참)");
        
        OnStateChanged?.Invoke(currentState);
        OnDungeonBagChanged?.Invoke(dungeonBag);
    }

    /// <summary>
    /// 던전 내 임시 가방(DungeonBag)에 전리품 추가
    /// </summary>
    public void AddLootInDungeon(int itemId, int quantity)
    {
        if (currentState != GameState.Dungeon)
        {
            Debug.LogWarning("[TownDungeonLoopManager] 던전 상태가 아닐 때는 전리품을 획득할 수 없습니다.");
            return;
        }

        if (quantity <= 0) return;

        var item = GetItemData(itemId);
        int maxStack = item != null ? item.maxStack : 99;

        dungeonBag.AddItem(itemId, quantity, 0, maxStack);
        Debug.Log($"[TownDungeonLoopManager] 전리품 획득: ID {itemId} ({item?.itemName ?? "Unknown"}) x{quantity}");

        OnDungeonBagChanged?.Invoke(dungeonBag);
    }

    /// <summary>
    /// 던전 내에서 임시 골드 획득
    /// </summary>
    public void AddGoldInDungeon(int amount)
    {
        if (currentState != GameState.Dungeon)
        {
            Debug.LogWarning("[TownDungeonLoopManager] 던전 상태가 아닐 때는 골드를 획득할 수 없습니다.");
            return;
        }

        dungeonBag.AddGold(amount);
        Debug.Log($"[TownDungeonLoopManager] 던전 골드 획득: +{amount}G (누적: {dungeonBag.acquiredGold}G)");

        OnDungeonBagChanged?.Invoke(dungeonBag);
    }

    /// <summary>
    /// 가방(bagItems)에 있는 장비 타입 아이템을 장착 슬롯(equippedItems)으로 이동
    /// 이미 같은 부위나 장비를 끼고 있다면 기존 장비는 가방으로 되돌림
    /// </summary>
    public bool EquipItemFromBag(int bagSlotIndex)
    {
        SyncEquippedItems();

        if (bagSlotIndex < 0 || bagSlotIndex >= dungeonBag.bagItems.Count)
        {
            Debug.LogWarning($"[TownDungeonLoopManager] 유효하지 않은 가방 슬롯 인덱스입니다: {bagSlotIndex}");
            return false;
        }

        var slotToEquip = dungeonBag.bagItems[bagSlotIndex];
        var itemData = GetItemData(slotToEquip.itemId);
        if (itemData == null || itemData.itemType != ItemType.Equipment)
        {
            Debug.LogWarning($"[TownDungeonLoopManager] 장비 타입(ItemType.Equipment) 아이템만 장착할 수 있습니다. (ID: {slotToEquip.itemId})");
            return false;
        }

        int equipItemId = slotToEquip.itemId;
        int equipEnhanceLevel = slotToEquip.enhanceLevel;

        // 1. 가방에서 해당 아이템 1개 차감
        if (slotToEquip.quantity > 1)
        {
            slotToEquip.quantity--;
        }
        else
        {
            dungeonBag.bagItems.RemoveAt(bagSlotIndex);
        }

        // 2. 이미 같은 부위나 동일 장비를 장착 중인지 확인
        string targetCategory = GetEquipmentCategory(itemData);
        int existingIndex = dungeonBag.equippedItems.FindIndex(slot =>
        {
            if (slot.itemId == equipItemId) return true; // 동일 장비
            var existingData = GetItemData(slot.itemId);
            return existingData != null && GetEquipmentCategory(existingData) == targetCategory; // 동일 부위
        });

        // 기존 장비가 있다면 가방으로 되돌림
        if (existingIndex >= 0)
        {
            var existingSlot = dungeonBag.equippedItems[existingIndex];
            dungeonBag.equippedItems.RemoveAt(existingIndex);

            var existingData = GetItemData(existingSlot.itemId);
            int maxStack = existingData != null ? existingData.maxStack : 1;
            dungeonBag.AddItem(existingSlot.itemId, existingSlot.quantity, existingSlot.enhanceLevel, maxStack);

            Debug.Log($"[TownDungeonLoopManager] 기존 장착 장비 해제 후 가방으로 반환: {existingData?.itemName ?? existingSlot.itemId.ToString()} (+{existingSlot.enhanceLevel})");
        }

        // 3. 신규 장비 장착 슬롯에 추가
        dungeonBag.equippedItems.Add(new InventorySlot(equipItemId, 1, equipEnhanceLevel));
        Debug.Log($"[TownDungeonLoopManager] 장비 장착 완료: {itemData.itemName} (+{equipEnhanceLevel})");

        OnDungeonBagChanged?.Invoke(dungeonBag);
        OnAccountDataChanged?.Invoke(accountData);
        OnEquippedItemsChanged?.Invoke(dungeonBag.equippedItems);
        return true;
    }

    /// <summary>
    /// 장착 중인 장비(equippedItems)를 다시 가방(bagItems)으로 해제
    /// </summary>
    public bool UnequipItemToBag(int equipSlotIndex)
    {
        SyncEquippedItems();

        if (equipSlotIndex < 0 || equipSlotIndex >= dungeonBag.equippedItems.Count)
        {
            Debug.LogWarning($"[TownDungeonLoopManager] 유효하지 않은 장착 슬롯 인덱스입니다: {equipSlotIndex}");
            return false;
        }

        var slotToUnequip = dungeonBag.equippedItems[equipSlotIndex];
        dungeonBag.equippedItems.RemoveAt(equipSlotIndex);

        var itemData = GetItemData(slotToUnequip.itemId);
        int maxStack = itemData != null ? itemData.maxStack : 1;
        dungeonBag.AddItem(slotToUnequip.itemId, slotToUnequip.quantity, slotToUnequip.enhanceLevel, maxStack);

        Debug.Log($"[TownDungeonLoopManager] 장비 해제 완료: {itemData?.itemName ?? slotToUnequip.itemId.ToString()} (+{slotToUnequip.enhanceLevel}) -> 가방으로 반환");

        OnDungeonBagChanged?.Invoke(dungeonBag);
        OnAccountDataChanged?.Invoke(accountData);
        OnEquippedItemsChanged?.Invoke(dungeonBag.equippedItems);
        return true;
    }

    /// <summary>
    /// 장비 아이템의 부위(무기, 방어구, 액세서리 등) 식별
    /// </summary>
    private string GetEquipmentCategory(ItemData item)
    {
        if (item == null) return "Unknown";

        string name = item.itemName ?? "";

        // 이름 키워드 기반 분류
        if (name.Contains("검") || name.Contains("소드") || name.Contains("지팡이") ||
            name.Contains("활") || name.Contains("도끼") || name.Contains("창") ||
            name.Contains("무기") || name.Contains("단검") || name.Contains("블레이드"))
        {
            return "Weapon";
        }
        if (name.Contains("갑옷") || name.Contains("방패") || name.Contains("투구") ||
            name.Contains("아머") || name.Contains("쉴드") || name.Contains("헬름") ||
            name.Contains("옷") || name.Contains("부츠") || name.Contains("로브"))
        {
            return "Armor";
        }
        if (name.Contains("반지") || name.Contains("목걸이") || name.Contains("팔찌") ||
            name.Contains("링") || name.Contains("아뮬렛") || name.Contains("부적"))
        {
            return "Accessory";
        }

        // ID 대역 기반 분류 (200대: 무기, 300대: 방어구, 400대: 액세서리 등)
        int categoryId = item.itemId / 100;
        if (categoryId == 2) return "Weapon";
        if (categoryId == 3) return "Armor";
        if (categoryId == 4) return "Accessory";

        return $"Equipment_{item.itemId}";
    }

    /// <summary>
    /// 플레이어 전멸 처리:
    /// - 골드 10% 차감(소수점 버림)
    /// - 장착 슬롯(equippedItems) 아이템은 전멸 페널티에서 완전히 제외되어 100% 보존
    /// - 일반 가방(bagItems) 비장착 아이템만 개별 수량 단위로 펼친 뒤 무작위 셔플하여 절반(totalCount / 2)을 잃어버림
    /// - 콘솔 로그(Debug.Log)에 "장착 장비는 안전하게 보존됨"과 함께 잃어버린 골드, 잃어버린 가방 아이템 목록 출력
    /// </summary>
    public void OnPlayerDefeated()
    {
        SyncEquippedItems();

        // 1. 보유 골드 10% 차감 (소수점 버림) 및 던전 획득 임시 골드 리셋
        int lostGold = accountData.gold > 0 ? accountData.gold / 10 : 0;
        accountData.gold -= lostGold;
        dungeonBag.acquiredGold = 0;

        // 2. 장착 슬롯(equippedItems)은 전멸 페널티에서 완전히 제외되어 100% 보존됨

        // 3. 일반 가방(bagItems)의 비장착 아이템들을 개별 수량 단위로 펼침
        List<InventorySlot> flattenedUnits = new List<InventorySlot>();
        if (dungeonBag.bagItems != null)
        {
            foreach (var slot in dungeonBag.bagItems)
            {
                if (slot == null || slot.quantity <= 0) continue;
                for (int i = 0; i < slot.quantity; i++)
                {
                    flattenedUnits.Add(new InventorySlot(slot.itemId, 1, slot.enhanceLevel));
                }
            }
        }

        int totalCount = flattenedUnits.Count;
        int loseCount = totalCount / 2; // 절반 (소수점 버림)

        // Fisher-Yates 무작위 셔플
        for (int i = flattenedUnits.Count - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);
            var temp = flattenedUnits[i];
            flattenedUnits[i] = flattenedUnits[randomIndex];
            flattenedUnits[randomIndex] = temp;
        }

        // 잃어버릴 아이템과 유지할 아이템 분리
        List<InventorySlot> lostUnits = flattenedUnits.GetRange(0, loseCount);
        List<InventorySlot> keptUnits = flattenedUnits.GetRange(loseCount, totalCount - loseCount);

        // 보존할 아이템들로 bagItems 재구성
        dungeonBag.bagItems.Clear();
        foreach (var kept in keptUnits)
        {
            var itemData = GetItemData(kept.itemId);
            int maxStack = itemData != null ? itemData.maxStack : 99;
            dungeonBag.AddItem(kept.itemId, kept.quantity, kept.enhanceLevel, maxStack);
        }

        // 4. 잃어버린 아이템 목록 집계 및 Console 로그(Debug.Log) 출력
        Dictionary<string, int> lostSummary = new Dictionary<string, int>();
        foreach (var lost in lostUnits)
        {
            var itemData = GetItemData(lost.itemId);
            string displayName = itemData != null ? itemData.itemName : $"아이템(ID:{lost.itemId})";
            if (lost.enhanceLevel > 0)
            {
                displayName += $" (+{lost.enhanceLevel})";
            }

            if (lostSummary.ContainsKey(displayName))
            {
                lostSummary[displayName] += lost.quantity;
            }
            else
            {
                lostSummary[displayName] = lost.quantity;
            }
        }

        StringBuilder logSb = new StringBuilder();
        logSb.AppendLine($"[TownDungeonLoopManager] 탐험 실패(전멸)! 장착 장비는 안전하게 보존됨.");
        logSb.AppendLine($"잃어버린 골드: {lostGold}G (남은 골드: {accountData.gold}G)");
        logSb.AppendLine($"잃어버린 가방 아이템 목록 (총 {lostUnits.Count}개):");
        if (lostSummary.Count > 0)
        {
            foreach (var pair in lostSummary)
            {
                logSb.AppendLine($"- {pair.Key} x{pair.Value}");
            }
        }
        else
        {
            logSb.AppendLine("- 없음");
        }

        Debug.Log(logSb.ToString().TrimEnd());

        // 5. 상태를 Town으로 복귀 및 이벤트 발행
        currentDungeonId = -1;
        currentState = GameState.Town;

        OnStateChanged?.Invoke(currentState);
        OnAccountDataChanged?.Invoke(accountData);
        OnDungeonBagChanged?.Invoke(dungeonBag);
    }

    /// <summary>
    /// 던전 탈출 성공 처리: 가방과 지참 골드를 그대로 유지한 채 클리어 기록 갱신 후 Town으로 복귀
    /// </summary>
    public void OnDungeonEscaped(int dungeonId)
    {
        // 1. 던전 클리어 기록 추가
        if (!accountData.clearedDungeonIds.Contains(dungeonId))
        {
            accountData.clearedDungeonIds.Add(dungeonId);
            Debug.Log($"[TownDungeonLoopManager] 던전(ID: {dungeonId}) 최초 클리어 기록 갱신!");
        }

        currentDungeonId = -1;
        currentState = GameState.Town;

        Debug.Log($"[TownDungeonLoopManager] 던전(ID: {dungeonId}) 탈출 성공! 획득한 아이템과 골드가 가방에 그대로 보존되었습니다. (안전 보관을 원하면 마을 창고/은행에 맡기세요)");

        OnStateChanged?.Invoke(currentState);
        OnAccountDataChanged?.Invoke(accountData);
        OnDungeonBagChanged?.Invoke(dungeonBag);
    }

    /// <summary>
    /// 상점에서 아이템 구매: 지참 골드 우선 차감(부족분은 은행 골드에서 추가 차감) 후
    /// 출정 가방(DungeonBag) 우선 지급 (가방 만석 시 마을 창고로 오버플로우)
    /// </summary>
    public bool BuyItem(ItemData item, int quantity)
    {
        if (item == null || quantity <= 0)
        {
            Debug.LogWarning("[TownDungeonLoopManager] 구매할 아이템 정보가 유효하지 않습니다.");
            return false;
        }

        int totalCost = item.buyPrice * quantity;
        int totalAvailableGold = dungeonBag.acquiredGold + accountData.gold;

        // (1) 먼저 지참 골드 + 마을 은행 골드의 합계가 totalCost보다 적으면 실패 처리
        if (totalAvailableGold < totalCost)
        {
            Debug.LogWarning($"[TownDungeonLoopManager] 골드가 부족합니다 (필요: {totalCost}G, 보유 합계: {totalAvailableGold}G [지참: {dungeonBag.acquiredGold}G, 은행: {accountData.gold}G])");
            return false;
        }

        // (2) 지참 골드(DungeonBag)에서 먼저 차감하고, 지참 골드가 모자란 차액만큼만 마을 은행 골드(AccountData.gold)에서 추가 차감
        int fromBagGold = Mathf.Min(dungeonBag.acquiredGold, totalCost);
        dungeonBag.acquiredGold -= fromBagGold;

        int fromBankGold = totalCost - fromBagGold;
        accountData.gold -= fromBankGold;

        // (3) 결제 내역 Debug.Log 출력
        string paymentBreakdown = fromBankGold > 0
            ? $"지참 골드 {fromBagGold}G + 은행 골드 {fromBankGold}G 차감"
            : $"지참 골드 {fromBagGold}G 전액 차감";
        Debug.Log($"[TownDungeonLoopManager] 상점 결제 완료: {paymentBreakdown} (잔여 - 지참: {dungeonBag.acquiredGold}G, 은행: {accountData.gold}G)");

        // 1. 출정 가방(DungeonBag)에 우선 보관 시도 (최대 maxBagSlots = 20)
        int addedToBag = dungeonBag.AddItemWithLimit(item.itemId, quantity, 0, item.maxStack, maxBagSlots);
        int overflowToStorage = quantity - addedToBag;

        // 2. 가방에 더 이상 들어가지 못한 초과분은 마을 창고(AccountData)로 자동 보관
        if (overflowToStorage > 0)
        {
            accountData.AddStorageItem(item.itemId, overflowToStorage, 0, item.maxStack);
        }

        // 3. Unity Console에 명확한 보관 위치 로그 출력
        if (overflowToStorage == 0)
        {
            Debug.Log($"[TownDungeonLoopManager] 아이템 구매 보관 완료 (출정 가방): {item.itemName} x{quantity} (가방 슬롯: {dungeonBag.bagItems.Count}/{maxBagSlots})");
        }
        else if (addedToBag == 0)
        {
            Debug.Log($"[TownDungeonLoopManager] 출정 가방 슬롯이 가득 찼습니다 ({dungeonBag.bagItems.Count}/{maxBagSlots}). 구매한 아이템이 마을 창고로 자동 보관되었습니다: {item.itemName} x{quantity}");
        }
        else
        {
            Debug.Log($"[TownDungeonLoopManager] 아이템 구매 보관 완료 (가방 부분 보관 및 창고 오버플로우): {item.itemName} x{quantity} [가방: {addedToBag}개, 창고: {overflowToStorage}개] (가방 슬롯: {dungeonBag.bagItems.Count}/{maxBagSlots})");
        }

        OnAccountDataChanged?.Invoke(accountData);
        OnDungeonBagChanged?.Invoke(dungeonBag);
        return true;
    }

    /// <summary>
    /// 디버그 및 테스트용 마을 골드 충전
    /// </summary>
    public void AddTownGold(int amount)
    {
        if (accountData == null) accountData = new AccountData();
        accountData.gold += amount;
        Debug.Log($"[TownDungeonLoopManager] [디버그] 마을 골드 충전: +{amount}G (현재 보유 골드: {accountData.gold}G)");
        OnAccountDataChanged?.Invoke(accountData);
    }

    /// <summary>
    /// 지참 골드(DungeonBag.acquiredGold)에서 마을 은행 골드(AccountData.gold)로 안전하게 입금
    /// </summary>
    public bool DepositGold(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning("[TownDungeonLoopManager] 입금할 골드 금액은 0보다 커야 합니다.");
            return false;
        }

        if (currentState != GameState.Town)
        {
            Debug.LogWarning("[TownDungeonLoopManager] 마을(Town) 상태에서만 은행 입금을 이용할 수 있습니다.");
            return false;
        }

        if (dungeonBag.acquiredGold < amount)
        {
            Debug.LogWarning($"[TownDungeonLoopManager] 입금 실패: 지참 골드가 부족합니다. (요청: {amount}G / 보유 지참 골드: {dungeonBag.acquiredGold}G)");
            return false;
        }

        dungeonBag.acquiredGold -= amount;
        accountData.gold += amount;

        Debug.Log($"[TownDungeonLoopManager] [마을 은행 입금] {amount}G 입금 완료 (지참 골드: {dungeonBag.acquiredGold}G, 은행 보유: {accountData.gold}G)");

        OnAccountDataChanged?.Invoke(accountData);
        OnDungeonBagChanged?.Invoke(dungeonBag);
        return true;
    }

    /// <summary>
    /// 마을 은행 골드(AccountData.gold)에서 지참 골드(DungeonBag.acquiredGold)로 출금
    /// </summary>
    public bool WithdrawGold(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning("[TownDungeonLoopManager] 출금할 골드 금액은 0보다 커야 합니다.");
            return false;
        }

        if (currentState != GameState.Town)
        {
            Debug.LogWarning("[TownDungeonLoopManager] 마을(Town) 상태에서만 은행 출금을 이용할 수 있습니다.");
            return false;
        }

        if (accountData.gold < amount)
        {
            Debug.LogWarning($"[TownDungeonLoopManager] 출금 실패: 은행 골드가 부족합니다. (요청: {amount}G / 보유 은행 골드: {accountData.gold}G)");
            return false;
        }

        accountData.gold -= amount;
        dungeonBag.AddGold(amount);

        Debug.Log($"[TownDungeonLoopManager] [마을 은행 출금] {amount}G 출금 완료 (은행 보유: {accountData.gold}G, 지참 골드: {dungeonBag.acquiredGold}G)");

        OnAccountDataChanged?.Invoke(accountData);
        OnDungeonBagChanged?.Invoke(dungeonBag);
        return true;
    }

    // 기존 메서드 호환성 연결
    public bool MoveGoldToBag(int amount) => WithdrawGold(amount);
    public bool MoveGoldToAccount(int amount) => DepositGold(amount);

    /// <summary>
    /// ID로 상점에서 아이템 구매: 골드 차감 및 마을 창고로 아이템 지급
    /// </summary>
    public bool BuyItem(int itemId, int quantity)
    {
        var item = GetItemData(itemId);
        if (item == null)
        {
            Debug.LogWarning($"[TownDungeonLoopManager] 구매할 아이템(ID: {itemId}) 마스터 데이터를 찾을 수 없습니다.");
            return false;
        }
        return BuyItem(item, quantity);
    }

    /// <summary>
    /// 창고 특정 슬롯의 아이템을 출정 가방(DungeonBag)으로 이동
    /// </summary>
    public bool MoveStorageToBag(int storageSlotIndex, int quantity = 1)
    {
        if (storageSlotIndex < 0 || storageSlotIndex >= accountData.storageItems.Count)
        {
            Debug.LogWarning($"[TownDungeonLoopManager] 유효하지 않은 창고 슬롯 인덱스입니다: {storageSlotIndex}");
            return false;
        }
        if (quantity <= 0) return false;

        var slot = accountData.storageItems[storageSlotIndex];
        int moveCount = Mathf.Min(quantity, slot.quantity);
        int itemId = slot.itemId;
        int enhanceLevel = slot.enhanceLevel;

        var itemData = GetItemData(itemId);
        int maxStack = itemData != null ? itemData.maxStack : 99;

        if (accountData.RemoveStorageItem(storageSlotIndex, moveCount))
        {
            dungeonBag.AddItem(itemId, moveCount, enhanceLevel, maxStack);
            Debug.Log($"[TownDungeonLoopManager] 창고 → 가방 이동 완료: {itemData?.itemName ?? itemId.ToString()} x{moveCount}");

            OnAccountDataChanged?.Invoke(accountData);
            OnDungeonBagChanged?.Invoke(dungeonBag);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 출정 가방(DungeonBag) 특정 슬롯의 아이템을 창고로 이동
    /// </summary>
    public bool MoveBagToStorage(int bagSlotIndex, int quantity = 1)
    {
        if (bagSlotIndex < 0 || bagSlotIndex >= dungeonBag.bagItems.Count)
        {
            Debug.LogWarning($"[TownDungeonLoopManager] 유효하지 않은 가방 슬롯 인덱스입니다: {bagSlotIndex}");
            return false;
        }
        if (quantity <= 0) return false;

        var slot = dungeonBag.bagItems[bagSlotIndex];
        int moveCount = Mathf.Min(quantity, slot.quantity);
        int itemId = slot.itemId;
        int enhanceLevel = slot.enhanceLevel;

        var itemData = GetItemData(itemId);
        int maxStack = itemData != null ? itemData.maxStack : 99;

        if (dungeonBag.RemoveItem(bagSlotIndex, moveCount))
        {
            accountData.AddStorageItem(itemId, moveCount, enhanceLevel, maxStack);
            Debug.Log($"[TownDungeonLoopManager] 가방 → 창고 이동 완료: {itemData?.itemName ?? itemId.ToString()} x{moveCount}");

            OnAccountDataChanged?.Invoke(accountData);
            OnDungeonBagChanged?.Invoke(dungeonBag);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 가방에 있는 첫 번째 장비(ItemType.Equipment) 아이템을 찾아 장착 슬롯으로 이동
    /// </summary>
    public bool EquipFirstEquipmentFromBag()
    {
        if (dungeonBag == null || dungeonBag.bagItems == null) return false;

        for (int i = 0; i < dungeonBag.bagItems.Count; i++)
        {
            var slot = dungeonBag.bagItems[i];
            if (slot == null) continue;

            var itemData = GetItemData(slot.itemId);
            if (itemData != null && itemData.itemType == ItemType.Equipment)
            {
                return EquipItemFromBag(i);
            }
        }

        Debug.LogWarning("[TownDungeonLoopManager] 가방에 장착 가능한 장비(Equipment) 아이템이 없습니다.");
        return false;
    }

    /// <summary>
    /// 상점에 창고 아이템 판매: 슬롯에서 아이템 차감 및 골드 획득
    /// </summary>
    public bool SellItem(int slotIndex, int quantity)
    {
        if (slotIndex < 0 || slotIndex >= accountData.storageItems.Count)
        {
            Debug.LogWarning($"[TownDungeonLoopManager] 유효하지 않은 슬롯 인덱스입니다: {slotIndex}");
            return false;
        }

        var slot = accountData.storageItems[slotIndex];
        if (quantity <= 0 || slot.quantity < quantity)
        {
            Debug.LogWarning($"[TownDungeonLoopManager] 판매 수량이 부족합니다. (요청: {quantity} / 보유: {slot.quantity})");
            return false;
        }

        var item = GetItemData(slot.itemId);
        int unitPrice = item != null ? item.sellPrice : 0;
        int totalEarned = unitPrice * quantity;

        if (accountData.RemoveStorageItem(slotIndex, quantity))
        {
            accountData.gold += totalEarned;
            Debug.Log($"[TownDungeonLoopManager] 아이템 판매 완료: ID {slot.itemId} ({item?.itemName ?? "Unknown"}) x{quantity} (획득: {totalEarned}G, 현재 골드: {accountData.gold}G)");

            OnAccountDataChanged?.Invoke(accountData);
            return true;
        }

        return false;
    }
}
