using System;
using UnityEngine;

/// <summary>
/// 가방 및 창고의 개별 슬롯 데이터
/// </summary>
[Serializable]
public class InventorySlot
{
    [Tooltip("아이템 식별 ID")]
    public int itemId;

    [Tooltip("소지 수량")]
    public int quantity;

    [Tooltip("장비 강화 수치 (기본값 0)")]
    public int enhanceLevel;

    public InventorySlot()
    {
        itemId = 0;
        quantity = 0;
        enhanceLevel = 0;
    }

    public InventorySlot(int itemId, int quantity, int enhanceLevel = 0)
    {
        this.itemId = itemId;
        this.quantity = quantity;
        this.enhanceLevel = enhanceLevel;
    }

    /// <summary>
    /// 슬롯 깊은 복사
    /// </summary>
    public InventorySlot Clone()
    {
        return new InventorySlot(itemId, quantity, enhanceLevel);
    }
}
