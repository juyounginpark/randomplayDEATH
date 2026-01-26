using UnityEngine;
using TMPro;
using System.Collections;

public class Roulette : MonoBehaviour
{
    [Header("Roulette Settings")]
    [Tooltip("룰렛 회전할 오브젝트")]
    public Transform rouletteObject;

    [Tooltip("룰렛 시작 키")]
    public KeyCode spinKey = KeyCode.Space;

    [Tooltip("파티션 수 (기본 8개)")]
    public int partitionCount = 8;

    [Header("Spin Settings")]
    [Tooltip("최소 회전 수")]
    public int minSpins = 3;

    [Tooltip("최대 회전 수")]
    public int maxSpins = 5;

    [Tooltip("회전 시간 (초)")]
    public float spinDuration = 6f;

    [Tooltip("마지막 감속 정도 (높을수록 더 천천히, 간질간질하게)")]
    [Range(2f, 5f)]
    public float decelerationPower = 3.5f;

    [Header("UI Settings")]
    [Tooltip("결과 표시 TextMeshPro Text")]
    public TextMeshProUGUI resultText;

    [Tooltip("회전 중 메시지")]
    public string spinningMessage = "회전 중...";

    [Tooltip("결과 메시지 형식 ({0}에 숫자가 들어감)")]
    public string resultMessageFormat = "결과: {0}번";

    private bool isSpinning = false;
    private float partitionAngle;

    void Start()
    {
        // 파티션 각도 계산 (8개면 45도)
        partitionAngle = 360f / partitionCount;

        // 룰렛 오브젝트가 없으면 자기 자신 사용
        if (rouletteObject == null)
        {
            rouletteObject = transform;
        }

        // UI 초기화
        if (resultText != null)
        {
            resultText.text = "";
        }

        Debug.Log($"룰렛 초기화 - 파티션: {partitionCount}개, 파티션 각도: {partitionAngle}도");
    }

    void Update()
    {
        // 스페이스 키로 룰렛 시작
        if (Input.GetKeyDown(spinKey) && !isSpinning)
        {
            StartSpin();
        }
    }

    /// <summary>
    /// 룰렛 회전 시작
    /// </summary>
    public void StartSpin()
    {
        if (isSpinning)
        {
            Debug.Log("이미 회전 중입니다!");
            return;
        }

        StartCoroutine(SpinRoutine());
    }

    /// <summary>
    /// 룰렛 회전 코루틴
    /// </summary>
    private IEnumerator SpinRoutine()
    {
        isSpinning = true;

        // UI 업데이트
        if (resultText != null)
        {
            resultText.text = spinningMessage;
        }

        // 완전 랜덤 각도 생성 (0~360도)
        float randomAngle = Random.Range(0f, 360f);

        // 추가 회전 수 (최소 3바퀴에서 최대 5바퀴)
        int extraSpins = Random.Range(minSpins, maxSpins + 1);
        float totalRotation = (extraSpins * 360f) + randomAngle;

        Debug.Log($"룰렛 시작 - 목표 랜덤 각도: {randomAngle}도, 총 회전: {totalRotation}도");

        // 시작 각도 저장
        float startRotation = rouletteObject.eulerAngles.z;
        float endRotation = startRotation + totalRotation;

        // 회전 애니메이션
        float elapsedTime = 0f;

        while (elapsedTime < spinDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / spinDuration;

            // 커스텀 감속 커브 (마지막에 아주 천천히)
            // EaseOut 효과를 제곱으로 강화
            float curveValue = 1f - Mathf.Pow(1f - t, decelerationPower);

            // 현재 회전 각도 계산
            float currentRotation = Mathf.Lerp(startRotation, endRotation, curveValue);

            // Z축 회전 적용
            rouletteObject.rotation = Quaternion.Euler(0f, -90f, currentRotation);

            yield return null;
        }

        // 최종 각도로 정확히 설정
        rouletteObject.rotation = Quaternion.Euler(0f, -90f, endRotation);

        // 최종 각도를 0-360 범위로 정규화
        float finalAngle = endRotation % 360f;
        if (finalAngle < 0)
            finalAngle += 360f;

        // 각도를 기반으로 파티션 계산
        int resultPartition = GetPartitionFromAngle(finalAngle);

        // 결과 표시
        ShowResult(resultPartition);

        Debug.Log($"룰렛 종료 - 최종 각도: {finalAngle}도, 결과 파티션: {resultPartition}번");

        isSpinning = false;
    }

    /// <summary>
    /// 결과 표시
    /// </summary>
    private void ShowResult(int partition)
    {
        if (resultText != null)
        {
            resultText.text = string.Format(resultMessageFormat, partition);
        }

        Debug.Log($"룰렛 결과: {partition}번");
    }

    /// <summary>
    /// 각도에서 파티션 번호 계산
    /// </summary>
    private int GetPartitionFromAngle(float angle)
    {
        // 0-360 범위로 정규화
        angle = angle % 360f;
        if (angle < 0)
            angle += 360f;

        // 파티션 번호 계산 (1부터 시작)
        // 0도 = 1번, 45도 = 2번, 90도 = 3번 ...
        int partition = Mathf.FloorToInt(angle / partitionAngle) + 1;

        // 범위 체크
        if (partition > partitionCount)
            partition = 1;

        return partition;
    }

    /// <summary>
    /// 현재 각도에서 파티션 번호 계산 (디버그용)
    /// </summary>
    public int GetCurrentPartition()
    {
        if (rouletteObject == null)
            return -1;

        // 현재 Z축 회전 각도 가져오기
        float currentAngle = rouletteObject.eulerAngles.z;

        return GetPartitionFromAngle(currentAngle);
    }

    // 기즈모로 파티션 표시 (에디터에서만 보임)
    void OnDrawGizmosSelected()
    {
        if (rouletteObject == null)
            return;

        Gizmos.color = Color.yellow;
        Vector3 center = rouletteObject.position;

        // 각 파티션 경계선 그리기
        for (int i = 0; i < partitionCount; i++)
        {
            float angle = i * (360f / partitionCount) * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f);
            Gizmos.DrawLine(center, center + direction * 2f);

            // 파티션 번호 표시 위치
            Vector3 textPos = center + direction * 1.5f;

            #if UNITY_EDITOR
            UnityEditor.Handles.Label(textPos, (i + 1).ToString());
            #endif
        }
    }
}
