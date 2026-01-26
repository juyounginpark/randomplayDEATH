using UnityEngine;
using System.Collections;

[System.Serializable]
public class ItemData
{
    [Tooltip("아이템 이름")]
    public string itemName;

    [Tooltip("아이템 프리팹")]
    public GameObject itemPrefab;
}

public class Item : MonoBehaviour
{
    [Header("Item Database")]
    [Tooltip("아이템 데이터베이스 배열")]
    public ItemData[] itemDatabase;

    [Header("Current Item")]
    [Tooltip("현재 장착된 아이템 인덱스 (-1은 아이템 없음)")]
    public int currentItemIndex = -1;

    [Tooltip("아이템이 생성될 위치 (비어있으면 플레이어 위치)")]
    public Transform itemSpawnPoint;

    [Header("Input Settings")]
    [Tooltip("왼쪽 마우스 클릭으로 아이템 사용 (펀치/아이템 액션)")]
    public bool useMouseClick = true;

    [Header("Hand Punch Settings")]
    [Tooltip("손 오브젝트 (Player의 자식으로 있는 Hand를 드래그)")]
    public GameObject handObject;

    [Tooltip("펀치 거리")]
    public float punchDistance = 1.5f;

    [Tooltip("펀치 지속 시간")]
    public float punchDuration = 0.3f;

    private GameObject currentItemObject;
    private bool isPunching = false;
    private Vector3 handOriginalLocalPosition;

    void Start()
    {
        // 손의 원래 로컬 위치 저장
        if (handObject != null)
        {
            handOriginalLocalPosition = handObject.transform.localPosition;
            Debug.Log($"Hand 원래 위치 저장: {handOriginalLocalPosition}");
        }
        else
        {
            Debug.LogWarning("Hand Object가 할당되지 않았습니다!");
        }

        // 시작 시 현재 인덱스의 아이템 장착
        if (currentItemIndex >= 0 && currentItemIndex < itemDatabase.Length)
        {
            EquipItem(currentItemIndex);
        }
    }

    /// <summary>
    /// 특정 인덱스의 아이템을 장착합니다.
    /// </summary>
    /// <param name="itemIndex">장착할 아이템의 인덱스</param>
    public void EquipItem(int itemIndex)
    {
        // 유효한 인덱스인지 확인
        if (itemIndex < 0 || itemIndex >= itemDatabase.Length)
        {
            Debug.LogError($"잘못된 아이템 인덱스: {itemIndex}");
            return;
        }

        // 기존 아이템 제거
        if (currentItemObject != null)
        {
            Destroy(currentItemObject);
        }

        // 새 아이템 데이터 가져오기
        ItemData itemData = itemDatabase[itemIndex];

        if (itemData.itemPrefab == null)
        {
            Debug.LogError($"아이템 인덱스 {itemIndex}의 프리팹이 할당되지 않았습니다.");
            return;
        }

        // 새 아이템 생성
        Vector3 spawnPosition = itemSpawnPoint != null ? itemSpawnPoint.position : transform.position;
        Quaternion spawnRotation = itemSpawnPoint != null ? itemSpawnPoint.rotation : transform.rotation;

        currentItemObject = Instantiate(itemData.itemPrefab, spawnPosition, spawnRotation);

        // 아이템을 플레이어의 자식으로 설정 (따라다니게)
        if (itemSpawnPoint != null)
        {
            currentItemObject.transform.SetParent(itemSpawnPoint);
        }
        else
        {
            currentItemObject.transform.SetParent(transform);
        }

        // 현재 인덱스 업데이트
        currentItemIndex = itemIndex;

        Debug.Log($"아이템 장착: {itemData.itemName} (인덱스: {itemIndex})");
    }

    /// <summary>
    /// 현재 장착된 아이템을 해제합니다.
    /// </summary>
    public void UnequipItem()
    {
        if (currentItemObject != null)
        {
            Destroy(currentItemObject);
            currentItemObject = null;
            currentItemIndex = -1;
            Debug.Log("아이템 해제됨");
        }
    }

    /// <summary>
    /// 현재 아이템의 이름을 반환합니다.
    /// </summary>
    public string GetCurrentItemName()
    {
        if (currentItemIndex >= 0 && currentItemIndex < itemDatabase.Length)
        {
            return itemDatabase[currentItemIndex].itemName;
        }
        return "없음";
    }

    /// <summary>
    /// 다음 아이템으로 전환 (테스트용)
    /// </summary>
    public void NextItem()
    {
        if (itemDatabase.Length == 0) return;

        int nextIndex = (currentItemIndex + 1) % itemDatabase.Length;
        EquipItem(nextIndex);
    }

    /// <summary>
    /// 이전 아이템으로 전환 (테스트용)
    /// </summary>
    public void PreviousItem()
    {
        if (itemDatabase.Length == 0) return;

        int prevIndex = currentItemIndex - 1;
        if (prevIndex < 0)
            prevIndex = itemDatabase.Length - 1;

        EquipItem(prevIndex);
    }

    // 테스트용: 숫자 키로 아이템 전환
    void Update()
    {
        // 1~9 키로 아이템 전환
        for (int i = 0; i < 9 && i < itemDatabase.Length; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                EquipItem(i);
            }
        }

        // 0 키로 아이템 해제 (손으로 전환)
        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            UnequipItem();
        }

        // Q, E 키로 이전/다음 아이템 전환
        if (Input.GetKeyDown(KeyCode.Q))
        {
            PreviousItem();
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            NextItem();
        }

        // 왼쪽 마우스 클릭으로 펀치 또는 아이템 액션 실행
        if (useMouseClick && Input.GetMouseButtonDown(0))
        {
            UseItem();
        }
    }

    /// <summary>
    /// 아이템 사용 (펀치 또는 아이템 액션)
    /// </summary>
    public void UseItem()
    {
        // 아이템이 없으면 펀치
        if (currentItemIndex == -1 && !isPunching)
        {
            Punch();
        }
        // 아이템이 있으면 아이템 액션 실행
        else if (currentItemIndex >= 0 && currentItemIndex < itemDatabase.Length)
        {
            // 여기에 각 아이템별 액션 추가 가능
            Debug.Log($"{itemDatabase[currentItemIndex].itemName} 사용!");
        }
    }

    /// <summary>
    /// 손을 표시합니다.
    /// </summary>
    private void ShowHand()
    {
        if (handObject == null)
        {
            Debug.LogWarning("Hand Object가 할당되지 않았습니다.");
            return;
        }

        handObject.SetActive(true);
        Debug.Log("손 표시됨");
    }

    /// <summary>
    /// 손을 숨깁니다.
    /// </summary>
    private void HideHand()
    {
        if (handObject != null)
        {
            handObject.SetActive(false);
        }
    }

    /// <summary>
    /// 펀치를 실행합니다.
    /// </summary>
    public void Punch()
    {
        if (handObject == null)
        {
            Debug.LogWarning("Hand Object가 없습니다!");
            return;
        }

        if (!handObject.activeInHierarchy)
        {
            Debug.LogWarning("Hand Object가 비활성화되어 있습니다!");
            return;
        }

        if (isPunching)
        {
            Debug.Log("이미 펀치 중입니다!");
            return;
        }

        Debug.Log("펀치 시작!");
        StartCoroutine(PunchCoroutine());
    }

    /// <summary>
    /// 펀치 애니메이션 코루틴
    /// </summary>
    private IEnumerator PunchCoroutine()
    {
        isPunching = true;

        // 목표 위치 계산 (로컬 좌표계에서 앞으로 Z축 이동)
        Vector3 targetLocalPosition = handOriginalLocalPosition + new Vector3(0, 0, punchDistance);

        Debug.Log($"펀치 시작 - 원래 위치: {handOriginalLocalPosition}, 목표 위치: {targetLocalPosition}");

        float elapsedTime = 0f;
        float halfDuration = punchDuration / 2f;

        // 앞으로 이동 (펀치 날리기)
        while (elapsedTime < halfDuration)
        {
            if (handObject == null)
            {
                isPunching = false;
                yield break;
            }

            handObject.transform.localPosition = Vector3.Lerp(handOriginalLocalPosition, targetLocalPosition, elapsedTime / halfDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 위치 확정
        if (handObject != null)
        {
            handObject.transform.localPosition = targetLocalPosition;
            Debug.Log($"펀치 최대 도달 - 현재 위치: {handObject.transform.localPosition}");
        }

        elapsedTime = 0f;

        // 뒤로 이동 (원래 위치로 돌아오기)
        while (elapsedTime < halfDuration)
        {
            if (handObject == null)
            {
                isPunching = false;
                yield break;
            }

            handObject.transform.localPosition = Vector3.Lerp(targetLocalPosition, handOriginalLocalPosition, elapsedTime / halfDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 원래 위치로 확정
        if (handObject != null)
        {
            handObject.transform.localPosition = handOriginalLocalPosition;
            Debug.Log($"펀치 완료 - 최종 위치: {handObject.transform.localPosition}");
        }

        isPunching = false;
    }
}
