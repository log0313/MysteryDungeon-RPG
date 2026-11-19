using System;
using System.Collections.Generic;
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

    // 상태 및 데이터 변화 알림 이벤트
    public event Action<GameState> OnStateChanged;
    public event Action<AccountData> OnAccountDataChanged;
    public event Action<DungeonBag> OnDungeonBagChanged;

    // 아이템 마스터 데이터 캐시
    private readonly Dictionary<int, ItemData> itemDatabase = new Dictionary<int, ItemData>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadItemDatabase();
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
    /// 던전 진입: 임시 가방(DungeonBag) 초기화 및 Dungeon 상태로 전환
    /// </summary>
    public void EnterDungeon(int dungeonId)
    {
        currentDungeonId = dungeonId;
        dungeonBag.Clear();
        currentState = GameState.Dungeon;

        Debug.Log($"[TownDungeonLoopManager] 던전(ID: {dungeonId})에 진입했습니다. DungeonBag이 초기화되었습니다.");
        
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
    /// 플레이어 전멸 처리: 임시 가방 파기(Clear), 상태를 Town으로 복귀
    /// </summary>
    public void OnPlayerDefeated()
    {
        Debug.LogWarning($"[TownDungeonLoopManager] 탐험 실패(전멸)! 던전(ID: {currentDungeonId})에서 획득한 모든 전리품이 소실되었습니다.");

        dungeonBag.Clear();
        currentDungeonId = -1;
        currentState = GameState.Town;

        OnStateChanged?.Invoke(currentState);
        OnDungeonBagChanged?.Invoke(dungeonBag);
    }

    /// <summary>
    /// 던전 탈출 성공 처리: 가방의 골드/전리품을 AccountData로 Commit, 클리어 기록 갱신 후 Town으로 복귀
    /// </summary>
    public void OnDungeonEscaped(int dungeonId)
    {
        Debug.Log($"[TownDungeonLoopManager] 던전(ID: {dungeonId}) 탈출 성공! 전리품 정산을 진행합니다.");

        // 1. 임시 가방 데이터를 계정 창고로 정산/병합
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
