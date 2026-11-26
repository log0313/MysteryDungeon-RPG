using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 포켓몬 불가사의 던전 스타일의 절차적 던전 맵 생성기 및 2D 타일 시각화 렌더러
/// </summary>
public class GridGenerator : MonoBehaviour
{
    [Header("맵 크기 설정")]
    [SerializeField] private int width = 40;
    [SerializeField] private int height = 30;

    [Header("방 생성 설정")]
    [SerializeField] private int minRooms = 5;
    [SerializeField] private int maxRooms = 8;
    [SerializeField] private Vector2Int minRoomSize = new Vector2Int(4, 4);
    [SerializeField] private Vector2Int maxRoomSize = new Vector2Int(8, 6);
    [SerializeField] private int roomPadding = 2;
    [SerializeField] private int maxPlacementAttempts = 200;

    [Header("타일 시각화 설정")]
    [Tooltip("미할당 시 런타임 기본 2D Square 스프라이트를 자동 생성해 사용합니다.")]
    [SerializeField] private Sprite tileSprite;
    [SerializeField] private float tileVisualScale = 0.96f;
    [SerializeField] private Color floorColor = new Color(0.84f, 0.80f, 0.70f, 1f);
    [SerializeField] private Color corridorColor = new Color(0.68f, 0.70f, 0.74f, 1f);
    [SerializeField] private Color wallColor = new Color(0.16f, 0.19f, 0.30f, 1f);
    [SerializeField] private Color stairsColor = new Color(0.18f, 0.88f, 0.85f, 1f);

    [Header("던전 진행 및 루프 연동")]
    [SerializeField] private int dungeonId = 1;
    [SerializeField] private int currentFloor = 1;
    [SerializeField] private bool generateOnStart = true;

    public DungeonGridData GridData { get; private set; }
    public int CurrentFloor => currentFloor;
    public int DungeonId => dungeonId;

    /// <summary>
    /// 던전 생성 완료 시 발행되는 이벤트 (그리드 데이터, 현재 층수)
    /// </summary>
    public event Action<DungeonGridData, int> OnDungeonGenerated;

    private Transform tileContainer;
    private readonly List<GameObject> spawnedTiles = new List<GameObject>();
    private Sprite runtimeFallbackSprite;

    private void Start()
    {
        SyncWithLoopManager();

        if (generateOnStart)
        {
            GenerateDungeon();
        }
    }

    private void OnDestroy()
    {
        if (runtimeFallbackSprite != null)
        {
            if (runtimeFallbackSprite.texture != null)
                Destroy(runtimeFallbackSprite.texture);
            Destroy(runtimeFallbackSprite);
        }
    }

    /// <summary>
    /// 기존 TownDungeonLoopManager 상태와 자연스럽게 동기화
    /// </summary>
    private void SyncWithLoopManager()
    {
        var loopManager = TownDungeonLoopManager.Instance;
        if (loopManager == null)
            return;

        if (loopManager.CurrentState != GameState.Dungeon)
        {
            loopManager.EnterDungeon(dungeonId);
        }
        else if (loopManager.CurrentDungeonId > 0)
        {
            dungeonId = loopManager.CurrentDungeonId;
        }
    }

    /// <summary>
    /// 기존 타일을 정리하고 현재 층의 절차적 던전을 새로 생성 및 렌더링
    /// </summary>
    public void GenerateDungeon()
    {
        ClearRenderedTiles();

        GridData = new DungeonGridData(width, height);

        GenerateRooms(GridData);
        ConnectRoomsWithCorridors(GridData);
        WrapWithWalls(GridData);
        AssignStartAndStairs(GridData);

        RenderGrid(GridData);

        Debug.Log($"[GridGenerator] {currentFloor}층 던전 생성 완료 (방 {GridData.rooms.Count}개, 시작: {GridData.startPos}, 계단: {GridData.stairsPos})");
        OnDungeonGenerated?.Invoke(GridData, currentFloor);
    }

    /// <summary>
    /// 층수를 1 증가시키고 다음 층 던전 생성
    /// </summary>
    public void AdvanceToNextFloor()
    {
        currentFloor++;
        GenerateDungeon();
    }

    /// <summary>
    /// 현재 층수 유지 상태로 던전 맵만 새로고침(재생성)
    /// </summary>
    public void RefreshCurrentFloor()
    {
        GenerateDungeon();
    }

    private void GenerateRooms(DungeonGridData data)
    {
        int targetRoomCount = UnityEngine.Random.Range(minRooms, maxRooms + 1);

        TryPlaceRooms(data, targetRoomCount, roomPadding, maxPlacementAttempts);

        // 패딩이 너무 커서 최소 방 개수에 못 미친 경우 패딩을 줄여 재시도
        if (data.rooms.Count < minRooms && roomPadding > 1)
        {
            TryPlaceRooms(data, targetRoomCount, 1, maxPlacementAttempts);
        }
    }

    private void TryPlaceRooms(DungeonGridData data, int targetCount, int padding, int attemptsLimit)
    {
        int attempts = 0;
        while (data.rooms.Count < targetCount && attempts < attemptsLimit)
        {
            attempts++;

            int roomW = UnityEngine.Random.Range(minRoomSize.x, maxRoomSize.x + 1);
            int roomH = UnityEngine.Random.Range(minRoomSize.y, maxRoomSize.y + 1);

            // 외곽 1칸은 벽(Wall) 배치를 위해 여유 확보
            int maxX = Mathf.Max(2, data.width - roomW);
            int maxY = Mathf.Max(2, data.height - roomH);

            int x = UnityEngine.Random.Range(1, maxX);
            int y = UnityEngine.Random.Range(1, maxY);

            RectInt candidate = new RectInt(x, y, roomW, roomH);
            if (DoesOverlapExistingRooms(data.rooms, candidate, padding))
                continue;

            data.rooms.Add(candidate);
            CarveRoom(data, candidate);
        }
    }

    private bool DoesOverlapExistingRooms(List<RectInt> existingRooms, RectInt candidate, int padding)
    {
        foreach (RectInt room in existingRooms)
        {
            RectInt padded = new RectInt(
                room.xMin - padding,
                room.yMin - padding,
                room.width + padding * 2,
                room.height + padding * 2
            );

            if (candidate.Overlaps(padded))
                return true;
        }
        return false;
    }

    private void CarveRoom(DungeonGridData data, RectInt room)
    {
        for (int x = room.xMin; x < room.xMax; x++)
        {
            for (int y = room.yMin; y < room.yMax; y++)
            {
                data.SetTile(new Vector2Int(x, y), TileType.Floor);
            }
        }
    }

    private void ConnectRoomsWithCorridors(DungeonGridData data)
    {
        if (data.rooms.Count < 2)
            return;

        for (int i = 0; i < data.rooms.Count - 1; i++)
        {
            Vector2Int fromCenter = GetRoomCenter(data.rooms[i]);
            Vector2Int toCenter = GetRoomCenter(data.rooms[i + 1]);
            CarveLShapedCorridor(data, fromCenter, toCenter);
        }
    }

    private void CarveLShapedCorridor(DungeonGridData data, Vector2Int from, Vector2Int to)
    {
        bool horizontalFirst = UnityEngine.Random.value < 0.5f;

        if (horizontalFirst)
        {
            CarveHorizontalLine(data, from.x, to.x, from.y);
            CarveVerticalLine(data, from.y, to.y, to.x);
        }
        else
        {
            CarveVerticalLine(data, from.y, to.y, from.x);
            CarveHorizontalLine(data, from.x, to.x, to.y);
        }
    }

    private void CarveHorizontalLine(DungeonGridData data, int startX, int endX, int y)
    {
        int min = Mathf.Min(startX, endX);
        int max = Mathf.Max(startX, endX);

        for (int x = min; x <= max; x++)
        {
            CarveCorridorCell(data, new Vector2Int(x, y));
        }
    }

    private void CarveVerticalLine(DungeonGridData data, int startY, int endY, int x)
    {
        int min = Mathf.Min(startY, endY);
        int max = Mathf.Max(startY, endY);

        for (int y = min; y <= max; y++)
        {
            CarveCorridorCell(data, new Vector2Int(x, y));
        }
    }

    private void CarveCorridorCell(DungeonGridData data, Vector2Int pos)
    {
        // 방의 바닥(Floor)은 유지하고 빈 공간(Void)만 복도(Corridor)로 파냄
        if (data.GetTile(pos) == TileType.Void)
        {
            data.SetTile(pos, TileType.Corridor);
        }
    }

    private void WrapWithWalls(DungeonGridData data)
    {
        for (int x = 0; x < data.width; x++)
        {
            for (int y = 0; y < data.height; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (data.GetTile(pos) != TileType.Void)
                    continue;

                if (HasAdjacentWalkableSurface(data, pos))
                {
                    data.SetTile(pos, TileType.Wall);
                }
            }
        }
    }

    private bool HasAdjacentWalkableSurface(DungeonGridData data, Vector2Int pos)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                TileType neighbor = data.GetTile(new Vector2Int(pos.x + dx, pos.y + dy));
                if (neighbor == TileType.Floor || neighbor == TileType.Corridor)
                    return true;
            }
        }
        return false;
    }

    private void AssignStartAndStairs(DungeonGridData data)
    {
        if (data.rooms.Count == 0)
            return;

        RectInt startRoom = data.rooms[0];
        data.startPos = GetRandomPointInRoom(startRoom);

        RectInt farthestRoom = FindFarthestRoom(data.rooms, startRoom);
        data.stairsPos = GetRandomPointInRoom(farthestRoom);

        // 방이 1개뿐인 예외 상황에서도 시작 지점과 계단이 겹치지 않도록 보장
        if (data.stairsPos == data.startPos && farthestRoom.width * farthestRoom.height > 1)
        {
            for (int x = farthestRoom.xMin; x < farthestRoom.xMax; x++)
            {
                for (int y = farthestRoom.yMin; y < farthestRoom.yMax; y++)
                {
                    Vector2Int candidate = new Vector2Int(x, y);
                    if (candidate != data.startPos)
                    {
                        data.stairsPos = candidate;
                        break;
                    }
                }
                if (data.stairsPos != data.startPos)
                    break;
            }
        }

        data.SetTile(data.stairsPos, TileType.Stairs);
    }

    private RectInt FindFarthestRoom(List<RectInt> rooms, RectInt originRoom)
    {
        Vector2 originCenter = originRoom.center;
        RectInt farthest = originRoom;
        float maxSqrDist = -1f;

        foreach (RectInt room in rooms)
        {
            float sqrDist = (room.center - originCenter).sqrMagnitude;
            if (sqrDist > maxSqrDist)
            {
                maxSqrDist = sqrDist;
                farthest = room;
            }
        }

        return farthest;
    }

    private Vector2Int GetRoomCenter(RectInt room)
    {
        return new Vector2Int(room.xMin + room.width / 2, room.yMin + room.height / 2);
    }

    private Vector2Int GetRandomPointInRoom(RectInt room)
    {
        int x = UnityEngine.Random.Range(room.xMin, room.xMax);
        int y = UnityEngine.Random.Range(room.yMin, room.yMax);
        return new Vector2Int(x, y);
    }

    private void RenderGrid(DungeonGridData data)
    {
        EnsureTileContainer();
        Sprite spriteToUse = GetOrCreateTileSprite();

        for (int x = 0; x < data.width; x++)
        {
            for (int y = 0; y < data.height; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                TileType type = data.GetTile(pos);
                if (type == TileType.Void)
                    continue;

                CreateTileObject(data, pos, type, spriteToUse);
            }
        }
    }

    private void CreateTileObject(DungeonGridData data, Vector2Int pos, TileType type, Sprite sprite)
    {
        GameObject tileObj = new GameObject($"Tile_{type}_{pos.x}_{pos.y}");
        tileObj.transform.SetParent(tileContainer, false);
        tileObj.transform.position = data.GridToWorld(pos, 0f);
        tileObj.transform.localScale = Vector3.one * tileVisualScale;

        SpriteRenderer sr = tileObj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = GetTileColor(type);
        sr.sortingOrder = GetTileSortingOrder(type);

        spawnedTiles.Add(tileObj);
    }

    private Color GetTileColor(TileType type)
    {
        switch (type)
        {
            case TileType.Floor:    return floorColor;
            case TileType.Corridor: return corridorColor;
            case TileType.Wall:     return wallColor;
            case TileType.Stairs:   return stairsColor;
            default:                return Color.clear;
        }
    }

    private int GetTileSortingOrder(TileType type)
    {
        switch (type)
        {
            case TileType.Stairs: return 2;
            case TileType.Wall:   return 1;
            default:              return 0;
        }
    }

    private void EnsureTileContainer()
    {
        if (tileContainer != null)
            return;

        Transform existing = transform.Find("TileContainer");
        if (existing != null)
        {
            tileContainer = existing;
            return;
        }

        GameObject containerObj = new GameObject("TileContainer");
        containerObj.transform.SetParent(transform, false);
        tileContainer = containerObj.transform;
    }

    private void ClearRenderedTiles()
    {
        for (int i = spawnedTiles.Count - 1; i >= 0; i--)
        {
            if (spawnedTiles[i] != null)
            {
                DestroyTileObject(spawnedTiles[i]);
            }
        }
        spawnedTiles.Clear();

        if (tileContainer != null)
        {
            for (int i = tileContainer.childCount - 1; i >= 0; i--)
            {
                DestroyTileObject(tileContainer.GetChild(i).gameObject);
            }
        }
    }

    private void DestroyTileObject(GameObject obj)
    {
        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }

    private Sprite GetOrCreateTileSprite()
    {
        if (tileSprite != null)
            return tileSprite;

        if (runtimeFallbackSprite == null)
        {
            const int size = 16;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }
            tex.SetPixels(pixels);
            tex.Apply();

            runtimeFallbackSprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                size
            );
            runtimeFallbackSprite.name = "RuntimeSquareTileSprite";
        }

        return runtimeFallbackSprite;
    }
}
