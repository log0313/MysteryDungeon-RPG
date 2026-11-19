using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 계정 영구 데이터 (마을 창고, 보유 골드, 던전 클리어 이력 등)
/// </summary>
[Serializable]
public class AccountData
{
    [Tooltip("유저 고유 ID")]
    public string userId = "player_001";

    [Tooltip("유저 닉네임")]
    public string nickname = "구조대원";

    [Tooltip("보유 골드 (초기값 1000)")]
    public int gold = 1000;

    [Tooltip("마을 창고 보관 아이템 목록")]
    public List<InventorySlot> storageItems = new List<InventorySlot>();

    [Tooltip("클리어한 던전 ID 목록")]
    public List<int> clearedDungeonIds = new List<int>();

    public AccountData()
    {
        userId = "player_001";
        nickname = "구조대원";
        gold = 1000;
        storageItems = new List<InventorySlot>();
        clearedDungeonIds = new List<int>();
    }

    public AccountData(string userId, string nickname, int initialGold = 1000)
    {
        this.userId = userId;
        this.nickname = nickname;
        this.gold = initialGold;
        this.storageItems = new List<InventorySlot>();
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
/// 던전 탐험 중 임시 세션 가방 데이터 (전리품, 획득 골드)
/// </summary>
[Serializable]
public class DungeonBag
{
    [Tooltip("던전 내에서 획득한 임시 골드")]
    public int acquiredGold;

    [Tooltip("던전 내에서 획득한 임시 아이템 목록")]
    public List<InventorySlot> bagItems = new List<InventorySlot>();

    public DungeonBag()
    {
        Clear();
    }

    /// <summary>
    /// 던전 진입 또는 전멸 시 임시 가방 데이터 파기(Clear)
    /// </summary>
    public void Clear()
    {
        acquiredGold = 0;
        bagItems.Clear();
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
    /// 던전 탈출 성공 시 호출: 임시 가방의 골드와 아이템을 계정 창고(AccountData)로 병합(Commit)
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

        // 3. 임시 가방 비우기
        Clear();
    }
}
