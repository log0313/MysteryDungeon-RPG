using UnityEngine;

/// <summary>
/// 2D Orthographic 시점으로 플레이어를 부드럽게 추적하는 던전 카메라 컨트롤러
/// </summary>
[RequireComponent(typeof(Camera))]
public class DungeonCameraFollow : MonoBehaviour
{
    [Header("추적 대상")]
    [SerializeField] private Transform target;

    [Header("카메라 설정")]
    [SerializeField] private float orthographicSize = 7f;
    [SerializeField] private float followSpeed = 12f;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

    private Camera cachedCamera;

    private void Awake()
    {
        ConfigureCamera();
    }

    private void Start()
    {
        if (target == null)
        {
            var player = FindFirstObjectByType<TurnBasedPlayerController>();
            if (player != null)
            {
                SetTarget(player.transform, snapImmediately: true);
            }
        }
        else
        {
            SnapToTarget();
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            var player = FindFirstObjectByType<TurnBasedPlayerController>();
            if (player != null)
                target = player.transform;
            else
                return;
        }

        Vector3 desiredPos = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPos, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
    }

    /// <summary>
    /// 추적 대상 변경 및 선택적 즉시 스냅
    /// </summary>
    public void SetTarget(Transform newTarget, bool snapImmediately = false)
    {
        target = newTarget;
        if (snapImmediately && target != null)
        {
            SnapToTarget();
        }
    }

    /// <summary>
    /// 카메라를 대상 위치로 즉시 이동
    /// </summary>
    public void SnapToTarget()
    {
        if (target == null)
            return;

        transform.position = target.position + offset;
    }

    private void ConfigureCamera()
    {
        if (cachedCamera == null)
            cachedCamera = GetComponent<Camera>();

        cachedCamera.orthographic = true;
        cachedCamera.orthographicSize = orthographicSize;
    }
}
