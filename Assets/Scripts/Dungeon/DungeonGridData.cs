using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 던전 그리드의 개별 타일 종류
/// </summary>
public enum TileType
{
    Void,
    Wall,
    Floor,
    Corridor,
    Stairs
}

/// <summary>
/// 절차적 생성된 던전 맵의 순수 그리드 데이터 모델
/// </summary>
[Serializable]
public class DungeonGridData
{
    public int width = 40;
    public int height = 30;

    public TileType[,] tiles;
    public List<RectInt> rooms = new List<RectInt>();

    public Vector2Int startPos;
    public Vector2Int stairsPos;

    public DungeonGridData(int width = 40, int height = 30)
    {
        this.width = Mathf.Max(1, width);
        this.height = Mathf.Max(1, height);
        tiles = new TileType[this.width, this.height];
        rooms = new List<RectInt>();
        startPos = Vector2Int.zero;
        stairsPos = Vector2Int.zero;
    }

    /// <summary>
    /// 좌표가 그리드 범위 내에 있는지 확인
    /// </summary>
    public bool IsInBounds(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
    }

    /// <summary>
    /// 특정 좌표의 타일 타입 반환 (범위 밖은 Void)
    /// </summary>
    public TileType GetTile(Vector2Int pos)
    {
        if (!IsInBounds(pos))
            return TileType.Void;

        return tiles[pos.x, pos.y];
    }

    /// <summary>
    /// 특정 좌표의 타일 타입 설정
    /// </summary>
    public void SetTile(Vector2Int pos, TileType type)
    {
        if (!IsInBounds(pos))
            return;

        tiles[pos.x, pos.y] = type;
    }

    /// <summary>
    /// 특정 좌표가 이동 가능한 타일(Floor, Corridor, Stairs)인지 반환
    /// </summary>
    public bool IsWalkable(Vector2Int pos)
    {
        if (!IsInBounds(pos))
            return false;

        TileType type = tiles[pos.x, pos.y];
        return type == TileType.Floor || type == TileType.Corridor || type == TileType.Stairs;
    }

    /// <summary>
    /// 불가사의 던전 규칙에 따라 이동 가능 여부 판단 (대각선 이동 시 코너 벽 끼임 방지 포함)
    /// </summary>
    public bool CanMove(Vector2Int from, Vector2Int dir)
    {
        if (dir == Vector2Int.zero)
            return false;

        Vector2Int target = from + dir;
        if (!IsWalkable(target))
            return false;

        // 대각선 이동 시 인접한 두 직교 타일 중 하나라도 이동 불가(벽 등)면 차단
        if (dir.x != 0 && dir.y != 0)
        {
            Vector2Int orthoX = new Vector2Int(from.x + dir.x, from.y);
            Vector2Int orthoY = new Vector2Int(from.x, from.y + dir.y);

            if (!IsWalkable(orthoX) || !IsWalkable(orthoY))
                return false;
        }

        return true;
    }

    /// <summary>
    /// 1유닛 = 1칸 기준 그리드 좌표를 월드 좌표로 변환
    /// </summary>
    public Vector3 GridToWorld(Vector2Int pos, float z = 0f)
    {
        return new Vector3(pos.x, pos.y, z);
    }
}
