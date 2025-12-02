using System.Collections.Generic;
using System.Collections; 
using UnityEngine;

public class RockManager : MonoBehaviour
{
    [Header("Rock Parent & Spawn")]
    [Tooltip("돌들이 자식으로 붙을 루트. 비워두면 이 GameObject 사용")]
    public Transform rockRoot;

    [Tooltip("돌이 떨어지기 시작할 기준 위치. 비워두면 rockRoot 위치 사용")]
    public Transform spawnPoint;

    [Tooltip("맨 위 돌에서 이만큼 위에서 스폰 (m)")]
    public float spawnHeightAboveTop = 0.5f;

    [Header("Stack Settings")]
    [Tooltip("최대 몇 개까지 쌓일 수 있는지")]
    public int maxStackCount = 4;

    [Tooltip("최대 개수에 도달했을 때 얼마 뒤에 삭제할지 (초)")]
    public float stackClearDelay = 10f;

    [Header("Prefabs (0,1) ~ (2,2) 총 8개 코드")]
    [Tooltip("코드 8개에 대응하는 돌 프리팹 8개")]
    public GameObject[] rockPrefabs;   // 길이 8 추천

    [Tooltip("체크하면 코드 무시하고 항상 랜덤 프리팹 사용")]
    public bool useRandomPrefab = false;

    [Header("현재 코드 (0~2, 0~2)")]
    [Range(0, 2)] public int currentCodeX = 0;
    [Range(0, 2)] public int currentCodeY = 1;   // (0,1)부터가 유효니까 기본값 0,1

    // 내부 캐시용 (쌓인 돌들의 Collider들)
    private readonly List<Collider> _rockColliders = new List<Collider>();

    // 쌓인 돌 GameObject들
    private readonly List<GameObject> _rockObjects = new List<GameObject>();

    // 현재 스택 클리어 코루틴
    private Coroutine _stackClearCoroutine;     

    private void Awake()
    {
        if (rockRoot == null)
            rockRoot = transform;
    }

    // Flask / 다른 코드에서 코드 바꿔줄 때 호출
    public void SetCode(int x, int y)
    {
        currentCodeX = Mathf.Clamp(x, 0, 2);
        currentCodeY = Mathf.Clamp(y, 0, 2);
    }

    // 트리거에서 그냥 이 함수만 호출하면 “현재 코드 기준으로 돌 하나 쌓기”
    public void TriggerSpawn()
    {
        SpawnRockByCurrentCode();
    }

    // 코드까지 같이 넘기고 싶으면 이걸 호출
    public void TriggerSpawnWithCode(int x, int y)
    {
        SetCode(x, y);
        SpawnRockByCurrentCode();
    }

    // 스폰 로직
    private void SpawnRockByCurrentCode()
    {
          Debug.Log($"[RockManager] Spawn 호출됨. stack={_rockObjects.Count}, code=({currentCodeX},{currentCodeY})");

        // 먼저 null(이미 파괴된) Rock 정리
        CleanupRockList();

        // 이미 최대 개수 이상이면 더 이상 스폰 안 함
        if (_rockObjects.Count >= maxStackCount)
        {
            Debug.Log("[RockManager] 이미 최대 스택 수에 도달해서 더 이상 스폰하지 않습니다.");
            return;
        }

        if (rockPrefabs == null || rockPrefabs.Length == 0)
        {
            Debug.LogWarning("[RockManager] rockPrefabs가 비어 있습니다.");
            return;
        }

        GameObject prefab = null;

        // 프리팹 선택
        if (useRandomPrefab)
        {
            int r = Random.Range(0, rockPrefabs.Length);
            prefab = rockPrefabs[r];
        }
        else
        {
            int idx = CodeToIndex(currentCodeX, currentCodeY); // 0~7
            if (idx < 0 || idx >= rockPrefabs.Length)
            {
                Debug.LogWarning($"[RockManager] 코드({currentCodeX},{currentCodeY})에 해당하는 프리팹이 없습니다.");
                return;
            }
            prefab = rockPrefabs[idx];
        }

        // 스폰 위치 계산
        Vector3 basePos = (spawnPoint != null) ? spawnPoint.position : rockRoot.position;
        float topY = GetTopY();
        float spawnY = (topY <= basePos.y) ? basePos.y : topY + spawnHeightAboveTop;

        Vector3 spawnPos = new Vector3(basePos.x, spawnY, basePos.z);
        Quaternion spawnRot = prefab.transform.rotation * Quaternion.Euler(90f, 0f, 0f);;

        // 프리팹 생성
        GameObject rock = Instantiate(prefab, spawnPos, spawnRot, rockRoot);

        // Rigidbody 없으면 붙여서 중력 적용
        var rb = rock.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = rock.AddComponent<Rigidbody>();
        }
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.linearDamping = 10f; 
        rb.angularDamping = 0.5f;

        // 쌓인 돌 Colliders 캐싱
        var cols = rock.GetComponentsInChildren<Collider>();
        _rockColliders.AddRange(cols);
        _rockObjects.Add(rock);

        // 만약 최대 스택 개수에 도달했다면, 10초 후 삭제 코루틴 시작
        if (_rockObjects.Count == maxStackCount)
        {
            // 혹시 이전 코루틴이 돌고 있으면 정지
            if (_stackClearCoroutine != null)
                StopCoroutine(_stackClearCoroutine);

            _stackClearCoroutine = StartCoroutine(StackClearAfterDelay());
        }
    }

    /// <summary>
    /// null이 된 Rock들을 리스트에서 정리
    /// </summary>
    private void CleanupRockList()
    {
        _rockObjects.RemoveAll(r => r == null);
        _rockColliders.RemoveAll(c => c == null);
    }

    /// <summary>
    /// 최대 개수까지 쌓이고 난 뒤 stackClearDelay 만큼 기다렸다가 모두 삭제
    /// </summary>
    private IEnumerator StackClearAfterDelay()
    {
        yield return new WaitForSeconds(stackClearDelay);

        // 돌 모두 파괴
        foreach (var rock in _rockObjects)
        {
            if (rock != null)
            {
                Destroy(rock);
            }
        }

        _rockObjects.Clear();
        _rockColliders.Clear();

        _stackClearCoroutine = null;
    }

    /// <summary>
    /// 지금까지 쌓인 돌들 중 가장 위에 있는 Collider의 Y 값
    /// </summary>
    private float GetTopY()
    {
        float maxY = rockRoot.position.y;
        _rockColliders.RemoveAll(c => c == null);

        foreach (var col in _rockColliders)
        {
            if (!col.enabled) continue;
            float y = col.bounds.max.y;
            if (y > maxY) maxY = y;
        }

        return maxY;
    }

    /// <summary>
    /// (0,1)~(2,2) 8개 코드를 0~7 인덱스로 매핑
    /// (0,0)은 사용 안 함 → -1
    /// </summary>
    private int CodeToIndex(int x, int y)
    {
        x = Mathf.Clamp(x, 0, 2);
        y = Mathf.Clamp(y, 0, 2);

        if (x == 0 && y == 0) return -1;   // 안 쓰는 코드

        // (0,1) (0,2) (1,0) (1,1) (1,2) (2,0) (2,1) (2,2) → 0~7
        if (x == 0 && y == 1) return 0;
        if (x == 0 && y == 2) return 1;
        if (x == 1 && y == 0) return 2;
        if (x == 1 && y == 1) return 3;
        if (x == 1 && y == 2) return 4;
        if (x == 2 && y == 0) return 5;
        if (x == 2 && y == 1) return 6;
        if (x == 2 && y == 2) return 7;

        return -1;
    }
}
