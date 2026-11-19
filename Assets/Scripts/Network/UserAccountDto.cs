using System;
using System.Collections.Generic;

namespace RogueLike.Network
{
    /// <summary>
    /// 서버-클라이언트 통신용 유저 계정 데이터 DTO
    /// </summary>
    [Serializable]
    public class UserAccountDto
    {
        public string userId;
        public string nickname;
        public int gold;
        public List<InventorySlotDto> storageItems = new List<InventorySlotDto>();
        public List<int> clearedDungeonIds = new List<int>();

        public UserAccountDto()
        {
            storageItems = new List<InventorySlotDto>();
            clearedDungeonIds = new List<int>();
        }

        public static UserAccountDto FromRuntime(AccountData account)
        {
            if (account == null) return null;

            var dto = new UserAccountDto
            {
                userId = account.userId,
                nickname = account.nickname,
                gold = account.gold,
                clearedDungeonIds = new List<int>(account.clearedDungeonIds)
            };

            foreach (var slot in account.storageItems)
            {
                if (slot != null)
                {
                    dto.storageItems.Add(InventorySlotDto.FromRuntime(slot));
                }
            }

            return dto;
        }

        public AccountData ToRuntime()
        {
            var account = new AccountData(userId, nickname, gold)
            {
                clearedDungeonIds = new List<int>(clearedDungeonIds)
            };

            foreach (var slotDto in storageItems)
            {
                if (slotDto != null)
                {
                    account.storageItems.Add(slotDto.ToRuntime());
                }
            }

            return account;
        }
    }
}
