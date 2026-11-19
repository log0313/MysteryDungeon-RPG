using UnityEngine;

/// <summary>
/// 정적 아이템 마스터 데이터 (ScriptableObject)
/// </summary>
[CreateAssetMenu(fileName = "NewItemData", menuName = "MysteryDungeon/Item Data", order = 1)]
public class ItemData : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("아이템 고유 식별자")]
    public int itemId;

    [Tooltip("아이템 이름")]
    public string itemName;

    [Tooltip("아이템 종류")]
    public ItemType itemType;

    [Tooltip("아이템 설명")]
    [TextArea(2, 5)]
    public string description;

    [Header("가격 및 스택")]
    [Tooltip("상점 구매 가격")]
    public int buyPrice;

    [Tooltip("상점 판매 가격")]
    public int sellPrice;

    [Tooltip("최대 중첩 개수 (소비/재료는 99, 장비는 1 권장)")]
    public int maxStack = 99;

    /// <summary>
    /// 에디터 또는 스크립트에서 초기화하기 위한 편의 메서드
    /// </summary>
    public void Init(int id, string name, ItemType type, int buy, int sell, int stack, string desc)
    {
        itemId = id;
        itemName = name;
        itemType = type;
        buyPrice = buy;
        sellPrice = sell;
        maxStack = stack;
        description = desc;
    }
}
