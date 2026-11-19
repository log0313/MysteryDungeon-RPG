/// <summary>
/// 마을-던전 루프 게임 상태
/// </summary>
public enum GameState
{
    Town,       // 마을 상태 (상점 이용, 정비, 던전 입장 대기)
    Dungeon     // 던전 탐험 상태 (전투, 파밍, 탈출/전멸 이벤트 대기)
}
