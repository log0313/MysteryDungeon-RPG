using System;
using System.Collections.Generic;

namespace RogueLike.Network
{
    /// <summary>
    /// 백엔드 서버 연동을 위한 유저 데이터 서비스 인터페이스
    /// </summary>
    public interface IUserDataService
    {
        /// <summary>
        /// 계정 데이터 조회 (비동기 콜백)
        /// </summary>
        void LoadAccountData(string userId, Action<UserAccountDto> onSuccess, Action<string> onError);

        /// <summary>
        /// 계정 데이터 동기화/저장 (비동기 콜백)
        /// </summary>
        void SaveAccountData(UserAccountDto accountDto, Action onSuccess, Action<string> onError);

        /// <summary>
        /// 던전 탈출 결과 서버 정산 검증 (치트 방지 검증용)
        /// </summary>
        void VerifyDungeonClear(string userId, int dungeonId, int acquiredGold, List<InventorySlotDto> lootItems, Action<bool> onComplete);
    }
}
