using System;

namespace RogueLike.Network
{
    /// <summary>
    /// 서버-클라이언트 통신용 인벤토리 슬롯 DTO
    /// </summary>
    [Serializable]
    public class InventorySlotDto
    {
        public int itemId;
        public int quantity;
        public int enhanceLevel;

        public InventorySlotDto() { }

        public InventorySlotDto(int itemId, int quantity, int enhanceLevel)
        {
            this.itemId = itemId;
            this.quantity = quantity;
            this.enhanceLevel = enhanceLevel;
        }

        public static InventorySlotDto FromRuntime(InventorySlot slot)
        {
            if (slot == null) return null;
            return new InventorySlotDto(slot.itemId, slot.quantity, slot.enhanceLevel);
        }

        public InventorySlot ToRuntime()
        {
            return new InventorySlot(itemId, quantity, enhanceLevel);
        }
    }
}
