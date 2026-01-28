using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 카메라가 문을 바라보는 방향
/// </summary>
public enum CameraDirection
{
    Forward,  // glass의 forward 방향에서 봄
    Right,    // glass의 right 방향에서 봄 (왼쪽 측면)
    Left,     // glass의 left 방향에서 봄 (오른쪽 측면)
    Back      // glass의 back 방향에서 봄 (뒤에서)
}

/// <summary>
/// 문 데이터 구조체 (DoorHead에서 자동 생성됨)
///
/// 요구되는 프리팹 구조:
///   DoorHead (빈 오브젝트) - Inspector에 할당
///     └─ hinge - Y축 회전 담당
///         └─ Door - 실제 문 모델
///             └─ glass (선택사항) - 카메라가 비추는 타겟
/// </summary>
[System.Serializable]
public class DoorData
{
    [Tooltip("회전할 문 힌지 오브젝트 (Y축 회전)")]
    public Transform hinge;

    [Tooltip("카메라가 비추는 문 오브젝트 (Door)")]
    public Transform body;

    [Tooltip("카메라 타겟 (glass 또는 Door)")]
    public Transform cameraTarget;

    [HideInInspector]
    public bool isOpened = false;
}

public class GameFlow : MonoBehaviour
{
    [Header("Door Settings")]
    [Tooltip("DoorHead 프리팹들 (10개)\n구조: DoorHead(빈 오브젝트) > hinge > Door\n자동으로 hinge와 Door를 찾습니다")]
    public Transform[] doorHeads;

    // 자동으로 생성되는 문 데이터
    private DoorData[] doors;

    [Tooltip("문 닫힌 상태 Y 회전 각도")]
    [Range(0f, 180f)]
    public float closedYRotation = 36f;

    [Tooltip("문 열린 상태 Y 회전 각도")]
    [Range(0f, 180f)]
    public float openedYRotation = 108f;

    [Tooltip("문 여는 속도")]
    [Range(0.5f, 5f)]
    public float doorOpenSpeed = 2f;

    [Header("Camera Settings")]
    [Tooltip("메인 카메라")]
    public Camera mainCamera;

    [Tooltip("카메라가 문을 비추는 시간")]
    [Range(1f, 10f)]
    public float cameraFocusTime = 3f;

    [Tooltip("카메라 거리 (문으로부터)")]
    [Range(2f, 20f)]
    public float cameraDistance = 5f;

    [Tooltip("카메라 높이 오프셋")]
    [Range(0f, 5f)]
    public float cameraHeightOffset = 1.5f;

    [Tooltip("카메라 방향 설정 (Forward/Right/Left/Back)")]
    public CameraDirection cameraDirection = CameraDirection.Right;

    [Header("Countdown Settings")]
    [Tooltip("카운트다운 시간 (초)")]
    [Range(10f, 120f)]
    public float countdownTime = 30f;

    [Tooltip("카운트다운 UI Text (TextMeshPro)")]
    public TextMeshProUGUI countdownText;

    [Header("Roulette Settings")]
    [Tooltip("룰렛 오브젝트 (선택사항)")]
    public GameObject rouletteObject;

    // 카메라 원래 위치/회전 저장
    private Vector3 originalCameraPosition;
    private Quaternion originalCameraRotation;
    private Transform originalCameraParent;
    private CameraMove cameraMove;

    // 플레이어 제어
    private PlayerMove playerMove;

    // 진행 상태
    private bool isProcessing = false;
    private bool isCountdownActive = false;
    private int currentOpenDoorIndex = -1;

    [Header("Debug Settings")]
    [Tooltip("디버그 모드 (테스트 키 활성화)")]
    public bool debugMode = true;

    void Start()
    {
        // 메인 카메라 자동 찾기
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("Main Camera를 찾을 수 없습니다!");
                return;
            }
        }

        // CameraMove 컴포넌트 가져오기
        cameraMove = mainCamera.GetComponent<CameraMove>();

        // PlayerMove 컴포넌트 가져오기 (Player 태그로 찾기)
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            playerMove = playerObject.GetComponent<PlayerMove>();
            if (playerMove == null)
            {
                Debug.LogWarning("[GameFlow] Player에 PlayerMove 컴포넌트가 없습니다!");
            }
            else
            {
                Debug.Log($"[GameFlow] PlayerMove 연결 완료: {playerObject.name}");
            }
        }
        else
        {
            Debug.LogWarning("[GameFlow] Player 태그를 가진 오브젝트를 찾을 수 없습니다!");
        }

        // DoorHead에서 자동으로 DoorData 생성
        BuildDoorsFromDoorHeads();

        // 모든 문을 닫힌 상태로 초기화
        InitializeDoors();

        // 카운트다운 UI 초기화
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
            Debug.Log($"[GameFlow] 카운트다운 UI 설정 완료: {countdownText.name}");
        }
        else
        {
            Debug.LogWarning("[GameFlow] Countdown Text UI가 할당되지 않았습니다! Inspector에서 TextMeshProUGUI를 할당하세요.");
        }

        Debug.Log($"[GameFlow] 초기화 완료: {(doors != null ? doors.Length : 0)}개의 문");
        Debug.Log($"[GameFlow] 설정값 - closedYRotation: {closedYRotation}, openedYRotation: {openedYRotation}, countdownTime: {countdownTime}");
    }

    void Update()
    {
        // 디버그 모드: 테스트 키로 문 열기
        if (debugMode && !isProcessing && doors != null && doors.Length > 0)
        {
            // 1~9번 문
            for (int i = 0; i < Mathf.Min(doors.Length, 9); i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    Debug.Log($"[디버그] {i + 1}번 키 입력 - {i}번 문 열기 시도");
                    OpenDoorByRouletteResult(i);
                }
            }

            // 0키는 10번 문
            if (doors.Length >= 10 && Input.GetKeyDown(KeyCode.Alpha0))
            {
                Debug.Log($"[디버그] 0번 키 입력 - 9번 문 열기 시도");
                OpenDoorByRouletteResult(9);
            }
        }
    }

    /// <summary>
    /// DoorHead 배열에서 자동으로 DoorData 배열 생성
    /// 프리팹 구조:
    ///   DoorHead (빈 오브젝트)
    ///     └─ hinge (회전 축)
    ///         └─ Door (카메라가 비추는 문 오브젝트)
    /// </summary>
    private void BuildDoorsFromDoorHeads()
    {
        if (doorHeads == null || doorHeads.Length == 0)
        {
            Debug.LogError("doorHeads 배열이 비어있습니다! DoorHead 프리팹들을 할당해주세요.");
            doors = new DoorData[0];
            return;
        }

        doors = new DoorData[doorHeads.Length];

        for (int i = 0; i < doorHeads.Length; i++)
        {
            doors[i] = new DoorData();

            Transform doorHead = doorHeads[i];

            if (doorHead == null)
            {
                Debug.LogError($"doorHeads[{i}]가 null입니다!");
                continue;
            }

            // hinge 찾기 (DoorHead의 직접 자식)
            Transform hinge = doorHead.Find("hinge");
            if (hinge == null)
            {
                // 대소문자 구분 없이 다시 시도
                hinge = doorHead.Find("Hinge");
                if (hinge == null)
                {
                    Debug.LogError($"DoorHead[{i}] '{doorHead.name}'에서 'hinge' 또는 'Hinge' 자식을 찾을 수 없습니다!");
                    Debug.LogWarning($"DoorHead의 자식 오브젝트 목록:");
                    foreach (Transform child in doorHead)
                    {
                        Debug.LogWarning($"  - {child.name}");
                    }
                    continue;
                }
            }
            doors[i].hinge = hinge;

            // Door 찾기 (hinge의 자식)
            Transform door = hinge.Find("Door");
            if (door == null)
            {
                // 소문자도 시도
                door = hinge.Find("door");
                if (door == null)
                {
                    Debug.LogError($"hinge[{i}] '{hinge.name}'에서 'Door' 또는 'door' 자식을 찾을 수 없습니다!");
                    Debug.LogWarning($"hinge의 자식 오브젝트 목록:");
                    foreach (Transform child in hinge)
                    {
                        Debug.LogWarning($"  - {child.name}");
                    }
                    continue;
                }
            }
            doors[i].body = door;

            // glass 찾기 (Door의 자식, 선택사항)
            Transform glass = door.Find("glass");
            if (glass == null)
            {
                glass = door.Find("Glass");
            }

            // glass가 있으면 glass를 카메라 타겟으로, 없으면 Door를 타겟으로
            if (glass != null)
            {
                doors[i].cameraTarget = glass;
                Debug.Log($"[GameFlow] Door[{i}] 설정 완료: DoorHead='{doorHead.name}', hinge='{hinge.name}', Door='{door.name}', glass='{glass.name}' (카메라 타겟)");
            }
            else
            {
                doors[i].cameraTarget = door;
                Debug.Log($"[GameFlow] Door[{i}] 설정 완료: DoorHead='{doorHead.name}', hinge='{hinge.name}', Door='{door.name}' (glass 없음, Door를 카메라 타겟으로 사용)");
            }

            Debug.Log($"[GameFlow] Door[{i}] 위치 - hinge: {hinge.position}, Door: {door.position}, Target: {doors[i].cameraTarget.position}");
        }

        Debug.Log($"[GameFlow] 총 {doors.Length}개의 문이 자동으로 설정되었습니다.");

        // 유효성 검사
        int validDoors = 0;
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] != null && doors[i].hinge != null && doors[i].body != null && doors[i].cameraTarget != null)
            {
                validDoors++;
            }
        }
        Debug.Log($"[GameFlow] 유효한 문: {validDoors}/{doors.Length}");
    }


    /// <summary>
    /// 모든 문을 닫힌 상태로 초기화
    /// </summary>
    private void InitializeDoors()
    {
        Debug.Log($"[GameFlow] InitializeDoors 시작");

        if (doors == null || doors.Length == 0)
        {
            Debug.LogWarning("[GameFlow] doors 배열이 비어있습니다!");
            return;
        }

        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] == null)
            {
                Debug.LogWarning($"[GameFlow] doors[{i}]가 null입니다!");
                continue;
            }

            if (doors[i].hinge != null)
            {
                Vector3 rotation = doors[i].hinge.localEulerAngles;
                rotation.y = closedYRotation;
                doors[i].hinge.localEulerAngles = rotation;
                doors[i].isOpened = false;
                Debug.Log($"[GameFlow] Door[{i}] 초기화: 회전 {closedYRotation}도로 설정");
            }
            else
            {
                Debug.LogWarning($"[GameFlow] Door[{i}]: hinge가 할당되지 않았습니다!");
            }

            if (doors[i].body == null)
            {
                Debug.LogWarning($"[GameFlow] Door[{i}]: body가 할당되지 않았습니다!");
            }

            if (doors[i].cameraTarget == null)
            {
                Debug.LogWarning($"[GameFlow] Door[{i}]: cameraTarget이 할당되지 않았습니다!");
            }
        }

        Debug.Log($"[GameFlow] InitializeDoors 완료");
    }

    /// <summary>
    /// 룰렛 결과에 따라 문 열기 (외부에서 호출)
    /// </summary>
    /// <param name="doorIndex">열 문의 인덱스 (0부터 시작)</param>
    public void OpenDoorByRouletteResult(int doorIndex)
    {
        Debug.Log($"[GameFlow] OpenDoorByRouletteResult 호출됨: doorIndex={doorIndex}");

        if (doors == null || doors.Length == 0)
        {
            Debug.LogError("[GameFlow] doors 배열이 초기화되지 않았습니다!");
            return;
        }

        Debug.Log($"[GameFlow] doors 배열 크기: {doors.Length}, isProcessing: {isProcessing}, isCountdownActive: {isCountdownActive}");

        if (isProcessing)
        {
            Debug.LogWarning("[GameFlow] 이미 문을 여는 중입니다!");
            return;
        }

        if (isCountdownActive)
        {
            Debug.LogWarning("[GameFlow] 카운트다운이 진행 중입니다! 먼저 현재 문이 닫혀야 합니다.");
            return;
        }

        if (doorIndex < 0 || doorIndex >= doors.Length)
        {
            Debug.LogError($"[GameFlow] 잘못된 문 인덱스: {doorIndex} (총 {doors.Length}개의 문)");
            return;
        }

        Debug.Log($"[GameFlow] Door[{doorIndex}] 열기 시작 - hinge: {doors[doorIndex].hinge?.name}, body: {doors[doorIndex].body?.name}");
        OpenDoorByIndex(doorIndex);
    }

    /// <summary>
    /// 특정 인덱스의 문 열기
    /// </summary>
    private void OpenDoorByIndex(int index)
    {
        Debug.Log($"[GameFlow] OpenDoorByIndex 호출: index={index}");

        if (doors[index] == null)
        {
            Debug.LogError($"[GameFlow] Door[{index}]가 null입니다!");
            return;
        }

        if (doors[index].isOpened)
        {
            Debug.Log($"[GameFlow] Door[{index}]는 이미 열려있습니다.");
            return;
        }

        Debug.Log($"[GameFlow] Door[{index}] 코루틴 시작");
        StartCoroutine(OpenDoorSequence(index));
    }

    /// <summary>
    /// 문 열기 시퀀스 (카메라 이동 -> 문 열기 -> 대기 -> 카메라 복귀 -> 카운트다운)
    /// </summary>
    private IEnumerator OpenDoorSequence(int doorIndex)
    {
        Debug.Log($"[GameFlow] OpenDoorSequence 코루틴 시작: doorIndex={doorIndex}");

        isProcessing = true;
        currentOpenDoorIndex = doorIndex;

        DoorData door = doors[doorIndex];

        Debug.Log($"[GameFlow] Door[{doorIndex}] 열기 시작 - isProcessing=true");

        // 1. 원래 카메라 위치 저장
        Debug.Log($"[GameFlow] 1단계: 카메라 상태 저장");
        SaveCameraState();

        // 2. 플레이어 동작 정지 (이동, 점프 등 모두 차단)
        Debug.Log($"[GameFlow] 2단계: 플레이어 동작 정지");
        if (playerMove != null)
        {
            playerMove.FreezePlayer();
        }

        // 3. 카메라를 문으로 이동
        Debug.Log($"[GameFlow] 3단계: 카메라를 문으로 이동");
        yield return StartCoroutine(MoveCameraToDoor(door));

        // 4. 문 열기
        Debug.Log($"[GameFlow] 4단계: 문 열기");
        yield return StartCoroutine(OpenDoor(door));

        // 5. 잠시 대기 (문을 보여줌)
        Debug.Log($"[GameFlow] 5단계: {cameraFocusTime}초 대기");
        yield return new WaitForSeconds(cameraFocusTime);

        // 6. 카메라를 원래 위치로 복귀
        Debug.Log($"[GameFlow] 6단계: 카메라 복귀");
        yield return StartCoroutine(RestoreCameraPosition());

        // 7. 플레이어 동작 재개
        Debug.Log($"[GameFlow] 7단계: 플레이어 동작 재개");
        if (playerMove != null)
        {
            playerMove.UnfreezePlayer();
        }

        door.isOpened = true;
        isProcessing = false;

        Debug.Log($"[GameFlow] Door[{doorIndex}] 열기 완료 - isProcessing=false");

        // 8. 30초 카운트다운 시작
        Debug.Log($"[GameFlow] 8단계: 카운트다운 시작");
        StartCoroutine(CountdownAndCloseDoor(doorIndex));
    }

    /// <summary>
    /// 카메라 상태 저장
    /// </summary>
    private void SaveCameraState()
    {
        originalCameraPosition = mainCamera.transform.position;
        originalCameraRotation = mainCamera.transform.rotation;
        originalCameraParent = mainCamera.transform.parent;

        Debug.Log($"[GameFlow] 카메라 상태 저장: position={originalCameraPosition}, parent={originalCameraParent?.name ?? "null"}");

        // CameraMove 비활성화 (카메라 제어권 가져오기)
        if (cameraMove != null)
        {
            cameraMove.enabled = false;
            Debug.Log($"[GameFlow] CameraMove 비활성화");
        }
        else
        {
            Debug.LogWarning($"[GameFlow] CameraMove 컴포넌트가 null입니다.");
        }

        // 카메라 부모 해제 (독립적으로 이동)
        mainCamera.transform.SetParent(null);
        Debug.Log($"[GameFlow] 카메라 부모 해제 완료");
    }

    /// <summary>
    /// 카메라를 문으로 이동
    /// </summary>
    private IEnumerator MoveCameraToDoor(DoorData door)
    {
        Debug.Log($"[GameFlow] MoveCameraToDoor 시작 - 방향: {cameraDirection}");

        if (door.cameraTarget == null)
        {
            Debug.LogWarning("[GameFlow] door.cameraTarget이 없습니다. 카메라 이동 스킵");
            yield break;
        }

        // 카메라 타겟 (glass 또는 Door)
        Transform target = door.cameraTarget;

        // 목표 위치 계산
        Vector3 targetPosition = target.position;

        // 카메라 방향 벡터 계산 (선택한 방향에 따라)
        Vector3 directionVector = Vector3.zero;
        switch (cameraDirection)
        {
            case CameraDirection.Forward:
                directionVector = target.forward;
                Debug.Log($"[GameFlow] Forward 방향 사용: {directionVector}");
                break;
            case CameraDirection.Right:
                directionVector = target.right;
                Debug.Log($"[GameFlow] Right 방향 사용: {directionVector}");
                break;
            case CameraDirection.Left:
                directionVector = -target.right;
                Debug.Log($"[GameFlow] Left 방향 사용: {directionVector}");
                break;
            case CameraDirection.Back:
                directionVector = -target.forward;
                Debug.Log($"[GameFlow] Back 방향 사용: {directionVector}");
                break;
        }

        // 카메라 위치: 타겟에서 선택한 방향으로 거리만큼 떨어진 곳
        Vector3 cameraPosition = targetPosition + directionVector * cameraDistance;
        cameraPosition.y = targetPosition.y + cameraHeightOffset;

        // 목표 회전: 타겟을 바라보도록
        Vector3 lookDirection = targetPosition - cameraPosition;
        Quaternion targetRotation = Quaternion.LookRotation(lookDirection);

        Debug.Log($"[GameFlow] 타겟 정보: position={targetPosition}");
        Debug.Log($"[GameFlow] 방향 벡터: {directionVector}");
        Debug.Log($"[GameFlow] 카메라 이동: {originalCameraPosition} -> {cameraPosition}");
        Debug.Log($"[GameFlow] 카메라 회전: lookDirection={lookDirection}");

        float elapsed = 0f;
        float duration = 1f; // 카메라 이동 시간

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            mainCamera.transform.position = Vector3.Lerp(originalCameraPosition, cameraPosition, t);
            mainCamera.transform.rotation = Quaternion.Slerp(originalCameraRotation, targetRotation, t);

            yield return null;
        }

        mainCamera.transform.position = cameraPosition;
        mainCamera.transform.rotation = targetRotation;

        Debug.Log($"[GameFlow] MoveCameraToDoor 완료");
    }

    /// <summary>
    /// 문 열기 (회전)
    /// </summary>
    private IEnumerator OpenDoor(DoorData door)
    {
        Debug.Log($"[GameFlow] OpenDoor 시작");

        if (door.hinge == null)
        {
            Debug.LogWarning("[GameFlow] door.hinge가 없습니다. 문 열기 스킵");
            yield break;
        }

        Vector3 startRotation = door.hinge.localEulerAngles;
        Debug.Log($"[GameFlow] 문 시작 회전: {startRotation.y}도");

        float elapsed = 0f;
        float duration = 1f / doorOpenSpeed;

        Debug.Log($"[GameFlow] 문 회전 시작: {closedYRotation}도 -> {openedYRotation}도 (duration: {duration}초)");

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            // Y축만 회전
            Vector3 currentRotation = door.hinge.localEulerAngles;
            currentRotation.y = Mathf.LerpAngle(closedYRotation, openedYRotation, t);
            door.hinge.localEulerAngles = currentRotation;

            yield return null;
        }

        // 최종 각도 정확히 설정
        Vector3 finalRotation = door.hinge.localEulerAngles;
        finalRotation.y = openedYRotation;
        door.hinge.localEulerAngles = finalRotation;

        Debug.Log($"[GameFlow] OpenDoor 완료: 최종 회전 {door.hinge.localEulerAngles.y}도");
    }

    /// <summary>
    /// 문 닫기 (회전)
    /// </summary>
    private IEnumerator CloseDoor(DoorData door)
    {
        Debug.Log($"[GameFlow] CloseDoor 시작");

        if (door.hinge == null)
        {
            Debug.LogWarning("[GameFlow] door.hinge가 없습니다. 문 닫기 스킵");
            yield break;
        }

        float elapsed = 0f;
        float duration = 1f / doorOpenSpeed;

        Debug.Log($"[GameFlow] 문 회전 시작: {openedYRotation}도 -> {closedYRotation}도 (duration: {duration}초)");

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            // Y축 회전 (열린 상태 -> 닫힌 상태)
            Vector3 currentRotation = door.hinge.localEulerAngles;
            currentRotation.y = Mathf.LerpAngle(openedYRotation, closedYRotation, t);
            door.hinge.localEulerAngles = currentRotation;

            yield return null;
        }

        // 최종 각도 정확히 설정
        Vector3 finalRotation = door.hinge.localEulerAngles;
        finalRotation.y = closedYRotation;
        door.hinge.localEulerAngles = finalRotation;

        door.isOpened = false;

        Debug.Log($"[GameFlow] CloseDoor 완료: 최종 회전 {door.hinge.localEulerAngles.y}도");
    }

    /// <summary>
    /// 카운트다운 후 문 닫기
    /// </summary>
    private IEnumerator CountdownAndCloseDoor(int doorIndex)
    {
        Debug.Log($"[GameFlow] CountdownAndCloseDoor 시작: doorIndex={doorIndex}");

        isCountdownActive = true;

        // 카운트다운 UI 활성화
        if (countdownText != null)
        {
            Debug.Log($"[GameFlow] 카운트다운 UI 활성화");
            countdownText.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[GameFlow] countdownText가 null입니다! Inspector에서 TextMeshPro를 할당하세요.");
        }

        float remainingTime = countdownTime;

        while (remainingTime > 0f)
        {
            // UI 업데이트
            if (countdownText != null)
            {
                int seconds = Mathf.CeilToInt(remainingTime);
                countdownText.text = $"{seconds}";
            }

            yield return new WaitForSeconds(1f);
            remainingTime -= 1f;
        }

        // 카운트다운 종료
        if (countdownText != null)
        {
            countdownText.text = "문이 닫힙니다!";
            yield return new WaitForSeconds(1f);
            countdownText.gameObject.SetActive(false);
        }

        // 문 닫기
        Debug.Log($"[GameFlow] 문 닫기 시작: doorIndex={doorIndex}");
        if (doorIndex >= 0 && doorIndex < doors.Length && doors[doorIndex] != null)
        {
            yield return StartCoroutine(CloseDoor(doors[doorIndex]));
            Debug.Log($"[GameFlow] Door[{doorIndex}] 닫기 완료");
        }

        isCountdownActive = false;
        currentOpenDoorIndex = -1;
        Debug.Log($"[GameFlow] CountdownAndCloseDoor 종료");
    }

    /// <summary>
    /// 카메라를 원래 위치로 복귀
    /// </summary>
    private IEnumerator RestoreCameraPosition()
    {
        Debug.Log($"[GameFlow] RestoreCameraPosition 시작");

        float elapsed = 0f;
        float duration = 1f;

        Vector3 startPosition = mainCamera.transform.position;
        Quaternion startRotation = mainCamera.transform.rotation;

        Debug.Log($"[GameFlow] 카메라 복귀: {startPosition} -> {originalCameraPosition}");

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            mainCamera.transform.position = Vector3.Lerp(startPosition, originalCameraPosition, t);
            mainCamera.transform.rotation = Quaternion.Slerp(startRotation, originalCameraRotation, t);

            yield return null;
        }

        mainCamera.transform.position = originalCameraPosition;
        mainCamera.transform.rotation = originalCameraRotation;

        // 카메라 부모 복구
        mainCamera.transform.SetParent(originalCameraParent);
        Debug.Log($"[GameFlow] 카메라 부모 복구: {originalCameraParent?.name ?? "null"}");

        // CameraMove 재활성화
        if (cameraMove != null)
        {
            cameraMove.enabled = true;
            Debug.Log($"[GameFlow] CameraMove 재활성화");
        }

        Debug.Log($"[GameFlow] RestoreCameraPosition 완료");
    }

    /// <summary>
    /// 모든 문 닫기
    /// </summary>
    public void CloseAllDoors()
    {
        StopAllCoroutines();

        if (doors != null && doors.Length > 0)
        {
            InitializeDoors();
        }

        isProcessing = false;
        isCountdownActive = false;
        currentOpenDoorIndex = -1;

        // 카운트다운 UI 숨기기
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }

        // 카메라 복구 (중단된 경우를 위해)
        if (cameraMove != null && !cameraMove.enabled)
        {
            cameraMove.enabled = true;
        }

        // 플레이어 동작 재개 (혹시 freeze 상태였다면)
        if (playerMove != null && playerMove.IsFrozen())
        {
            playerMove.UnfreezePlayer();
        }

        Debug.Log("[GameFlow] 모든 문이 닫혔습니다.");
    }

    /// <summary>
    /// 특정 문이 열려있는지 확인
    /// </summary>
    public bool IsDoorOpened(int doorIndex)
    {
        if (doors == null || doorIndex < 0 || doorIndex >= doors.Length)
            return false;

        if (doors[doorIndex] == null)
            return false;

        return doors[doorIndex].isOpened;
    }

    /// <summary>
    /// 카운트다운이 진행 중인지 확인
    /// </summary>
    public bool IsCountdownActive()
    {
        return isCountdownActive;
    }

    /// <summary>
    /// 현재 열려있는 문의 인덱스 가져오기 (-1이면 열린 문 없음)
    /// </summary>
    public int GetCurrentOpenDoorIndex()
    {
        return currentOpenDoorIndex;
    }

    /// <summary>
    /// 현재 열려있는 문을 즉시 닫기 (카운트다운 취소)
    /// </summary>
    public void ForceCloseDoor()
    {
        if (currentOpenDoorIndex >= 0 && currentOpenDoorIndex < doors.Length)
        {
            StopAllCoroutines();
            StartCoroutine(ForceCloseDoorCoroutine(currentOpenDoorIndex));
        }
    }

    /// <summary>
    /// 문 강제 닫기 코루틴
    /// </summary>
    private IEnumerator ForceCloseDoorCoroutine(int doorIndex)
    {
        // 카운트다운 UI 숨기기
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }

        // 플레이어 동작 재개
        if (playerMove != null && playerMove.IsFrozen())
        {
            playerMove.UnfreezePlayer();
        }

        // 문 닫기
        if (doors[doorIndex] != null)
        {
            yield return StartCoroutine(CloseDoor(doors[doorIndex]));
            Debug.Log($"[GameFlow] Door[{doorIndex}] 강제로 닫힘");
        }

        isProcessing = false;
        isCountdownActive = false;
        currentOpenDoorIndex = -1;
    }
}
