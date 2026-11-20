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
    /// 던전 진입: 임시 가방(DungeonBag) 초기화 및 Dungeon 상태로 전환 (장착 중인 장비는 유지됨)
    /// </summary>
    public void EnterDungeon(int dungeonId)
    {
        SyncEquippedItems();
        currentDungeonId = dungeonId;
        dungeonBag.Clear();
        currentState = GameState.Dungeon;

        Debug.Log($"[TownDungeonLoopManager] 던전(ID: {dungeonId})에 진입했습니다. DungeonBag이 초기화되었습니다. (장착 장비 {dungeonBag.equippedItems.Count}개 유지)");
        
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
    /// 던전 탈출 성공 처리: 가방의 골드/전리품을 AccountData로 Commit, 클리어 기록 갱신 후 Town으로 복귀
    /// </summary>
    public void OnDungeonEscaped(int dungeonId)
    {
        Debug.Log($"[TownDungeonLoopManager] 던전(ID: {dungeonId}) 탈출 성공! 전리품 정산을 진행합니다.");

        // 1. 임시 가방 데이터를 계정 창고로 정산/병합 (장착 장비는 유지됨)
        dungeonBag.Commit(accountData);

        // 2. 던전 클리어 기록 추가
        if (!accountData.clearedDungeonIds.Contains(dungeonId))
        {
            accountData.clearedDungeonIds.Add(dungeonId);
            Debug.Log($"[TownDungeonLoopManager] 던전(ID: {dungeonId}) 최초 클리어 기록 갱신!");
        }

        currentDungeonId = -1;
        currentState = GameState.Town;

        OnStateChanged?.Invoke(currentState);
        OnAccountDataChanged?.Invoke(accountData);
        OnDungeonBagChanged?.Invoke(dungeonBag);
    }

    /// <summary>
    /// 상점에서 아이템 구매: 골드 차감 및 마을 창고로 아이템 지급
    /// </summary>
    public bool BuyItem(ItemData item, int quantity)
    {
        if (item == null || quantity <= 0)
        {
            Debug.LogWarning("[TownDungeonLoopManager] 구매할 아이템 정보가 유효하지 않습니다.");
            return false;
        }

        int totalCost = item.buyPrice * quantity;
        if (accountData.gold < totalCost)
        {
            Debug.LogWarning($"[TownDungeonLoopManager] 골드가 부족합니다. (필요: {totalCost}G / 보유: {accountData.gold}G)");
            return false;
        }

        accountData.gold -= totalCost;
        accountData.AddStorageItem(item.itemId, quantity, 0, item.maxStack);

        Debug.Log($"[TownDungeonLoopManager] 아이템 구매 완료: {item.itemName} x{quantity} (지불: {totalCost}G, 잔여: {accountData.gold}G)");

        OnAccountDataChanged?.Invoke(accountData);
        return true;
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
