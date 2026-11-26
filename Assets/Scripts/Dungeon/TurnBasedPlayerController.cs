using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 그리드 좌표 기반 8방향 턴제 플레이어 이동 컨트롤러
/// </summary>
public class TurnBasedPlayerController : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private GridGenerator gridGenerator;
    [SerializeField] private DungeonCameraFollow cameraFollow;

    [Header("그리드 및 이동 설정")]
    [SerializeField] private Vector2Int gridPos;
    [SerializeField] private float moveDuration = 0.12f;
    [Tooltip("동시 대각선 키(예: W+D) 입력 시 미세한 프레임 차이를 보정하는 대기 시간(초)")]
    [SerializeField] private float diagonalGraceTime = 0.04f;

    [Header("임시 도트 캐릭터 비주얼")]
    [SerializeField] private Color playerTintColor = new Color(1f, 0.85f, 0.25f, 1f);
    [SerializeField] private int sortingOrder = 10;

    public Vector2Int GridPos => gridPos;
    public int TurnCount => turnCount;

    private int turnCount;
    private bool isMoving;
    private Coroutine moveCoroutine;

    // 대각선 동시 입력 보정용 버퍼 상태
    private bool hasPendingCardinalDir;
    private Vector2Int pendingCardinalDir;
    private float pendingElapsed;

    private SpriteRenderer spriteRenderer;
    private Sprite runtimePlayerSprite;

    private void Awake()
    {
        ResolveReferences();
        EnsurePlayerVisual();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (gridGenerator != null)
        {
            gridGenerator.OnDungeonGenerated += HandleDungeonGenerated;
        }
    }

    private void Start()
    {
        if (gridGenerator != null && gridGenerator.GridData != null)
        {
            SpawnAtStartPos(gridGenerator.GridData);
        }
    }

    private void OnDisable()
    {
        if (gridGenerator != null)
        {
            gridGenerator.OnDungeonGenerated -= HandleDungeonGenerated;
        }
    }

    private void OnDestroy()
    {
        if (runtimePlayerSprite != null)
        {
            if (runtimePlayerSprite.texture != null)
                Destroy(runtimePlayerSprite.texture);
            Destroy(runtimePlayerSprite);
        }
    }

    private void Update()
    {
        if (gridGenerator == null || gridGenerator.GridData == null)
            return;

        // TownDungeonLoopManager가 존재하는 경우 던전 상태일 때만 입력 처리
        if (TownDungeonLoopManager.Instance != null &&
            TownDungeonLoopManager.Instance.CurrentState != GameState.Dungeon)
        {
            return;
        }

        // 테스트용 R 키: 현재 층 맵 새로고침(재생성)
        if (WasRefreshKeyPressed())
        {
            CancelActiveMovement();
            Debug.Log($"[TurnBasedPlayerController] R 키 입력: {gridGenerator.CurrentFloor}층 던전 새로고침.");
            gridGenerator.RefreshCurrentFloor();
            return;
        }

        if (isMoving)
            return;

        ProcessMovementInput();
    }

    private void ResolveReferences()
    {
        if (gridGenerator == null)
            gridGenerator = FindFirstObjectByType<GridGenerator>();

        if (cameraFollow == null && Camera.main != null)
            cameraFollow = Camera.main.GetComponent<DungeonCameraFollow>();
    }

    private void HandleDungeonGenerated(DungeonGridData data, int floor)
    {
        CancelActiveMovement();
        SpawnAtStartPos(data);
    }

    private void SpawnAtStartPos(DungeonGridData data)
    {
        if (data == null)
            return;

        gridPos = data.startPos;
        transform.position = data.GridToWorld(gridPos, 0f);

        if (cameraFollow != null)
        {
            cameraFollow.SetTarget(transform, snapImmediately: true);
        }

        Debug.Log($"[TurnBasedPlayerController] {gridGenerator.CurrentFloor}층 시작 지점 스폰 완료: {gridPos}");
    }

    private void CancelActiveMovement()
    {
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }

        isMoving = false;
        ResetPendingDiagonal();
    }

    private void ProcessMovementInput()
    {
        Vector2Int inputDir = ReadMovementDirection(out bool anyKeyJustPressed);

        if (inputDir == Vector2Int.zero)
        {
            ResetPendingDiagonal();
            return;
        }

        // 대각선 입력이 바로 들어왔으면 즉시 이동 시도
        if (inputDir.x != 0 && inputDir.y != 0)
        {
            ResetPendingDiagonal();
            TryStep(inputDir);
            return;
        }

        // 단일 축 입력이 방금 눌렸을 때, W+D 같은 동시 대각선 입력의 프레임 차이를 짧게 보정
        if (!hasPendingCardinalDir)
        {
            if (anyKeyJustPressed && diagonalGraceTime > 0f)
            {
                hasPendingCardinalDir = true;
                pendingCardinalDir = inputDir;
                pendingElapsed = 0f;
                return;
            }

            TryStep(inputDir);
            return;
        }

        pendingElapsed += Time.deltaTime;
        if (pendingElapsed >= diagonalGraceTime || inputDir != pendingCardinalDir)
        {
            ResetPendingDiagonal();
            TryStep(inputDir);
        }
    }

    private void ResetPendingDiagonal()
    {
        hasPendingCardinalDir = false;
        pendingCardinalDir = Vector2Int.zero;
        pendingElapsed = 0f;
    }

    private void TryStep(Vector2Int dir)
    {
        DungeonGridData data = gridGenerator.GridData;
        if (!data.CanMove(gridPos, dir))
            return;

        Vector2Int targetPos = gridPos + dir;
        moveCoroutine = StartCoroutine(MoveStepCoroutine(targetPos));
    }

    private IEnumerator MoveStepCoroutine(Vector2Int targetPos)
    {
        isMoving = true;

        DungeonGridData data = gridGenerator.GridData;
        Vector3 startWorld = transform.position;
        Vector3 endWorld = data.GridToWorld(targetPos, 0f);

        gridPos = targetPos;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, moveDuration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(startWorld, endWorld, t);
            yield return null;
        }

        transform.position = endWorld;
        isMoving = false;
        moveCoroutine = null;

        turnCount++;
        Debug.Log($"[Turn {turnCount}] ({gridGenerator.CurrentFloor}층) 플레이어 이동 -> 좌표: {gridPos}");

        // 계단 도착 시 즉시 다음 층 생성
        if (gridPos == data.stairsPos || data.GetTile(gridPos) == TileType.Stairs)
        {
            Debug.Log($"[TurnBasedPlayerController] 계단({gridPos}) 도착! {gridGenerator.CurrentFloor + 1}층으로 내려갑니다.");
            gridGenerator.AdvanceToNextFloor();
        }
    }

    private Vector2Int ReadMovementDirection(out bool anyJustPressed)
    {
        int dx = 0;
        int dy = 0;
        anyJustPressed = false;

#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed || kb.numpad4Key.isPressed) dx -= 1;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed || kb.numpad6Key.isPressed) dx += 1;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed || kb.numpad2Key.isPressed) dy -= 1;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed || kb.numpad8Key.isPressed) dy += 1;

            // 넘패드 대각선(1, 3, 7, 9) 지원
            if (kb.numpad7Key.isPressed) { dx = -1; dy = 1; }
            else if (kb.numpad9Key.isPressed) { dx = 1; dy = 1; }
            else if (kb.numpad1Key.isPressed) { dx = -1; dy = -1; }
            else if (kb.numpad3Key.isPressed) { dx = 1; dy = -1; }

            anyJustPressed =
                kb.aKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame ||
                kb.wKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame ||
                kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame ||
                kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame;

            return new Vector2Int(Mathf.Clamp(dx, -1, 1), Mathf.Clamp(dy, -1, 1));
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.Keypad4)) dx -= 1;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.Keypad6)) dx += 1;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.Keypad2)) dy -= 1;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.Keypad8)) dy += 1;

        if (Input.GetKey(KeyCode.Keypad7)) { dx = -1; dy = 1; }
        else if (Input.GetKey(KeyCode.Keypad9)) { dx = 1; dy = 1; }
        else if (Input.GetKey(KeyCode.Keypad1)) { dx = -1; dy = -1; }
        else if (Input.GetKey(KeyCode.Keypad3)) { dx = 1; dy = -1; }

        anyJustPressed =
            Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D) ||
            Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.S) ||
            Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) ||
            Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow);
#endif

        return new Vector2Int(Mathf.Clamp(dx, -1, 1), Mathf.Clamp(dy, -1, 1));
    }

    private bool WasRefreshKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.rKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.R);
#else
        return false;
#endif
    }

    private void EnsurePlayerVisual()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        if (spriteRenderer.sprite == null)
        {
            runtimePlayerSprite = CreatePixelCharacterSprite();
            spriteRenderer.sprite = runtimePlayerSprite;
        }

        spriteRenderer.color = playerTintColor;
        spriteRenderer.sortingOrder = sortingOrder;
    }

    /// <summary>
    /// 2D 도트 캐릭터 느낌의 16x16 임시 원형 스프라이트 생성
    /// </summary>
    private Sprite CreatePixelCharacterSprite()
    {
        const int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outerRadius = 6.5f;
        float innerRadius = 5.2f;

        Color outlineColor = new Color(0.15f, 0.12f, 0.10f, 1f);
        Color bodyColor = Color.white;
        Color eyeColor = new Color(0.10f, 0.10f, 0.15f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                Color c = Color.clear;

                if (dist <= innerRadius)
                {
                    c = bodyColor;
                }
                else if (dist <= outerRadius)
                {
                    c = outlineColor;
                }

                // 도트 눈 표현
                if ((x == 5 || x == 10) && (y == 9 || y == 10))
                {
                    c = eyeColor;
                }

                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = "RuntimePlayerDotSprite";
        return sprite;
    }
}
