using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 계정 영구 데이터 (마을 창고, 보유 골드, 장착 장비, 던전 클리어 이력 등)
/// </summary>
[Serializable]
public class AccountData
{
    [Tooltip("유저 고유 ID")]
    public string userId = "player_001";

    [Tooltip("유저 닉네임")]
    public string nickname = "구조대원";

    [Tooltip("보유 골드 (초기값 5000)")]
    public int gold = 5000;

    [Tooltip("마을 창고 보관 아이템 목록")]
    public List<InventorySlot> storageItems = new List<InventorySlot>();

    [Tooltip("현재 장착 중인 장비 목록 (무기, 방어구, 액세서리 등)")]
    public List<InventorySlot> equippedItems = new List<InventorySlot>();

    [Tooltip("클리어한 던전 ID 목록")]
    public List<int> clearedDungeonIds = new List<int>();

    public AccountData()
    {
        userId = "player_001";
        nickname = "구조대원";
        gold = 5000;
        storageItems = new List<InventorySlot>();
        equippedItems = new List<InventorySlot>();
        clearedDungeonIds = new List<int>();
    }

    public AccountData(string userId, string nickname, int initialGold = 5000)
    {
        this.userId = userId;
        this.nickname = nickname;
        this.gold = initialGold;
        this.storageItems = new List<InventorySlot>();
        this.equippedItems = new List<InventorySlot>();
        this.clearedDungeonIds = new List<int>();
    }

    /// <summary>
    /// 창고에 아이템 추가 (중첩 가능 여부 및 maxStack 반영)
    /// </summary>
    public void AddStorageItem(int itemId, int quantity, int enhanceLevel = 0, int maxStack = 99)
    {
        if (quantity <= 0) return;

        // 중첩 가능한 아이템인 경우 기존 슬롯에 합산 시도
        if (maxStack > 1)
        {
            var existingSlot = storageItems.Find(slot => slot.itemId == itemId && slot.enhanceLevel == enhanceLevel && slot.quantity < maxStack);
            if (existingSlot != null)
            {
                int canAdd = maxStack - existingSlot.quantity;
                int added = Mathf.Min(canAdd, quantity);
                existingSlot.quantity += added;
                quantity -= added;
            }
        }

        // 남은 수량은 새 슬롯에 분할 추가
        while (quantity > 0)
        {
            int count = Mathf.Min(quantity, maxStack);
            storageItems.Add(new InventorySlot(itemId, count, enhanceLevel));
            quantity -= count;
        }
    }

    /// <summary>
    /// 창고 특정 슬롯의 아이템 제거/차감
    /// </summary>
    public bool RemoveStorageItem(int slotIndex, int quantity)
    {
        if (slotIndex < 0 || slotIndex >= storageItems.Count)
            return false;

        var slot = storageItems[slotIndex];
        if (slot.quantity < quantity)
            return false;

        slot.quantity -= quantity;
        if (slot.quantity <= 0)
        {
            storageItems.RemoveAt(slotIndex);
        }
        return true;
    }
}

/// <summary>
/// 던전 탐험 중 임시 세션 가방 데이터 (전리품, 획득 골드, 장착 장비)
/// </summary>
[Serializable]
public class DungeonBag
{
    [Tooltip("던전 내에서 획득한 임시 골드")]
    public int acquiredGold;

    [Tooltip("던전 내에서 획득한 임시 아이템 목록")]
    public List<InventorySlot> bagItems = new List<InventorySlot>();

    [Tooltip("현재 장착 중인 장비 목록")]
    public List<InventorySlot> equippedItems = new List<InventorySlot>();

    public DungeonBag()
    {
        Clear();
        equippedItems = new List<InventorySlot>();
    }

    /// <summary>
    /// 던전 진입 또는 세션 초기화 시 임시 가방 데이터 파기(Clear).
    /// 장착 중인 장비(equippedItems)는 마을-던전 이동 시에도 보존됩니다.
    /// </summary>
    public void Clear()
    {
        acquiredGold = 0;
        bagItems.Clear();
        // equippedItems는 전멸 및 이동 시 보존 대상이므로 초기화하지 않음.
    }

    /// <summary>
    /// 던전 내 획득 골드 추가
    /// </summary>
    public void AddGold(int amount)
    {
        if (amount > 0)
        {
            acquiredGold += amount;
        }
    }

    /// <summary>
    /// 던전 내 획득 전리품 추가
    /// </summary>
    public void AddItem(int itemId, int quantity, int enhanceLevel = 0, int maxStack = 99)
    {
        if (quantity <= 0) return;

        if (maxStack > 1)
        {
            var existingSlot = bagItems.Find(slot => slot.itemId == itemId && slot.enhanceLevel == enhanceLevel && slot.quantity < maxStack);
            if (existingSlot != null)
            {
                int canAdd = maxStack - existingSlot.quantity;
                int added = Mathf.Min(canAdd, quantity);
                existingSlot.quantity += added;
                quantity -= added;
            }
        }

        while (quantity > 0)
        {
            int count = Mathf.Min(quantity, maxStack);
            bagItems.Add(new InventorySlot(itemId, count, enhanceLevel));
            quantity -= count;
        }
    }

    /// <summary>
    /// 최대 슬롯 수(maxSlots) 제한을 두고 가방에 아이템 추가.
    /// 기존 슬롯 중첩 합산 및 남은 슬롯 내 신규 슬롯 생성을 진행하며,
    /// 실제로 가방에 수납된 아이템 수량을 반환합니다.
    /// </summary>
    public int AddItemWithLimit(int itemId, int quantity, int enhanceLevel = 0, int maxStack = 99, int maxSlots = 20)
    {
        if (quantity <= 0) return 0;

        int initialQuantity = quantity;

        // 1. 중첩 가능한 기존 슬롯에 합산 시도
        if (maxStack > 1)
        {
            foreach (var slot in bagItems)
            {
                if (slot.itemId == itemId && slot.enhanceLevel == enhanceLevel && slot.quantity < maxStack)
                {
                    int canAdd = maxStack - slot.quantity;
                    int added = Mathf.Min(canAdd, quantity);
                    slot.quantity += added;
                    quantity -= added;

                    if (quantity <= 0) break;
                }
            }
        }

        // 2. 남은 수량이 있고 가방 슬롯이 최대 슬롯 수 미만인 경우 신규 슬롯 추가
        while (quantity > 0 && bagItems.Count < maxSlots)
        {
            int count = Mathf.Min(quantity, maxStack);
            bagItems.Add(new InventorySlot(itemId, count, enhanceLevel));
            quantity -= count;
        }

        return initialQuantity - quantity; // 가방에 실제로 들어간 수량 반환
    }

    /// <summary>
    /// 가방 특정 슬롯의 아이템 제거/차감
    /// </summary>
    public bool RemoveItem(int slotIndex, int quantity)
    {
        if (slotIndex < 0 || slotIndex >= bagItems.Count)
            return false;

        var slot = bagItems[slotIndex];
        if (slot.quantity < quantity)
            return false;

        slot.quantity -= quantity;
        if (slot.quantity <= 0)
        {
            bagItems.RemoveAt(slotIndex);
        }
        return true;
    }

    /// <summary>
    /// 던전 탈출 성공 시 호출: 임시 가방의 골드와 아이템을 계정 창고(AccountData)로 병합(Commit).
    /// 장착 중인 장비는 계정 및 가방에 유지됩니다.
    /// </summary>
    public void Commit(AccountData accountData)
    {
        if (accountData == null)
        {
            Debug.LogError("[DungeonBag] Commit 실패: AccountData가 null입니다.");
            return;
        }

        // 1. 획득 골드 정산
        accountData.gold += acquiredGold;

        // 2. 획득 아이템 창고로 병합
        foreach (var item in bagItems)
        {
            accountData.AddStorageItem(item.itemId, item.quantity, item.enhanceLevel);
        }

        Debug.Log($"[DungeonBag] Commit 완료: {acquiredGold}G 및 {bagItems.Count}개 슬롯 아이템이 계정 창고로 정산되었습니다.");

        // 3. 임시 가방 비우기 (장착 장비는 유지)
        Clear();
    }
}
