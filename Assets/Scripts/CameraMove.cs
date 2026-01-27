using UnityEngine;

public class CameraMove : MonoBehaviour
{
    private Transform player;

    [Header("Camera Settings")]
    [Tooltip("카메라 이동 부드러움 정도 (높을수록 빠르게 추적)")]
    [Range(1f, 20f)]
    public float smoothSpeed = 10f;

    [Header("Isometric View Settings")]
    [Tooltip("아이소메트릭 뷰 X축 회전 각도")]
    [Range(0f, 90f)]
    public float isometricAngleX = 35.264f;

    [Tooltip("아이소메트릭 뷰 Y축 회전 각도")]
    [Range(0f, 360f)]
    public float isometricAngleY = 45f;

    [Header("Zoom Settings")]
    [Tooltip("최소 거리 (최대 확대)")]
    [Range(3f, 20f)]
    public float minDistance = 5f;

    [Tooltip("최대 거리 (최대 축소, 기본값)")]
    [Range(10f, 100f)]
    public float maxDistance = 30f;

    [Tooltip("줌 속도")]
    [Range(0.5f, 20f)]
    public float zoomSpeed = 2f;

    [Tooltip("줌 부드러움")]
    [Range(1f, 20f)]
    public float zoomSmoothness = 8f;

    [Header("First Person Settings")]
    [Tooltip("1인칭 카메라 위치 (플레이어의 자식 오브젝트로 배치)")]
    public Transform firstPersonCameraPosition;

    [Tooltip("1인칭 전환 임계값 (minDistance보다 작거나 같으면 전환)")]
    [Range(0.1f, 2f)]
    public float firstPersonThreshold = 0.5f;

    [Header("Rotation Settings")]
    [Tooltip("마우스 오른쪽 버튼으로 회전 활성화 (3인칭)")]
    public bool enableMouseRotation = true;

    [Tooltip("마우스 회전 속도")]
    [Range(0.1f, 10f)]
    public float rotationSpeed = 3f;

    [Tooltip("회전 부드러움 (높을수록 빠르게 회전)")]
    [Range(1f, 20f)]
    public float rotationSmoothness = 8f;

    [Tooltip("수직 회전 최소 각도")]
    [Range(0f, 89f)]
    public float minVerticalAngle = 10f;

    [Tooltip("수직 회전 최대 각도")]
    [Range(0f, 89f)]
    public float maxVerticalAngle = 80f;

    [Header("First Person Rotation Settings")]
    [Tooltip("1인칭 마우스 감도")]
    [Range(0.1f, 10f)]
    public float firstPersonSensitivity = 2f;

    [Tooltip("1인칭 수직 회전 최소 각도")]
    [Range(-89f, 0f)]
    public float firstPersonMinVerticalAngle = -80f;

    [Tooltip("1인칭 수직 회전 최대 각도")]
    [Range(0f, 89f)]
    public float firstPersonMaxVerticalAngle = 80f;

    private float currentDistance;
    private float targetDistance;
    private float currentHorizontalAngle;
    private float currentVerticalAngle; // 3인칭 수직 각도 동적 제어
    private float targetHorizontalAngle; // 목표 수평 각도
    private float targetVerticalAngle; // 목표 수직 각도
    private float firstPersonVerticalAngle = 0f;
    private float firstPersonHorizontalAngle = 0f;

    // 외부에서 접근 가능한 1인칭 모드 상태
    public bool IsFirstPersonMode { get; private set; } = false;

    // 캐싱된 변수들
    private Vector3 positionVelocity = Vector3.zero;
    private float horizontalAngleVelocity = 0f;
    private float verticalAngleVelocity = 0f;

    void Start()
    {
        // Player 태그를 가진 오브젝트 찾기
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;

            // 초기 각도 설정 (아이소메트릭 뷰)
            currentHorizontalAngle = isometricAngleY;
            currentVerticalAngle = isometricAngleX; // 초기값을 isometricAngleX로 설정
            targetHorizontalAngle = isometricAngleY;
            targetVerticalAngle = isometricAngleX;

            // 초기 회전 적용
            transform.rotation = Quaternion.Euler(currentVerticalAngle, currentHorizontalAngle, 0f);

            // 초기 거리 설정 (최대 거리로 시작)
            currentDistance = maxDistance;
            targetDistance = maxDistance;

            // 플레이어의 현재 위치 기준으로 카메라 초기 위치 설정
            Vector3 offset = CalculateOffset();
            transform.position = player.position + offset;

            Debug.Log($"카메라 초기 위치 설정: Player={player.position}, Camera={transform.position}, Rotation={transform.rotation.eulerAngles}, Distance={currentDistance}");
        }
        else
        {
            Debug.LogError("Player 태그를 가진 오브젝트를 찾을 수 없습니다!");
        }
    }

    void LateUpdate()
    {
        if (player == null)
            return;

        // 마우스 스크롤 입력으로 줌 조절
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (scrollInput != 0f)
        {
            targetDistance -= scrollInput * zoomSpeed;
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        }

        // 부드러운 줌 (프레임 독립적)
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, zoomSmoothness * Time.deltaTime);

        // 1인칭/3인칭 모드 전환 체크
        if (currentDistance <= minDistance + firstPersonThreshold)
        {
            // 1인칭 모드로 전환
            if (!IsFirstPersonMode)
            {
                IsFirstPersonMode = true;
                // 3인칭 각도를 1인칭 초기 각도로 설정
                firstPersonHorizontalAngle = currentHorizontalAngle;
                firstPersonVerticalAngle = 0f;

                // 마우스 커서 잠금
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                Debug.Log("1인칭 모드로 전환");
            }

            // 1인칭 마우스 입력 처리 (FPS 스타일)
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            firstPersonHorizontalAngle += mouseX * firstPersonSensitivity;
            firstPersonVerticalAngle -= mouseY * firstPersonSensitivity;
            firstPersonVerticalAngle = Mathf.Clamp(firstPersonVerticalAngle, firstPersonMinVerticalAngle, firstPersonMaxVerticalAngle);

            // 플레이어 수평 회전 동기화
            if (player != null)
            {
                Quaternion targetPlayerRotation = Quaternion.Euler(0f, firstPersonHorizontalAngle, 0f);
                Rigidbody playerRb = player.GetComponent<Rigidbody>();
                if (playerRb != null)
                {
                    // Rigidbody가 있으면 Slerp 후 MoveRotation 사용 (부드러운 회전)
                    Quaternion smoothRotation = Quaternion.Slerp(player.rotation, targetPlayerRotation, 20f * Time.deltaTime);
                    playerRb.MoveRotation(smoothRotation);
                }
                else
                {
                    // Rigidbody가 없으면 Slerp로 부드럽게 회전
                    player.rotation = Quaternion.Slerp(player.rotation, targetPlayerRotation, 20f * Time.deltaTime);
                }
            }

            // 1인칭 카메라 위치 설정
            Vector3 targetPosition;
            if (firstPersonCameraPosition != null)
            {
                // firstPersonCameraPosition의 월드 위치 사용
                targetPosition = firstPersonCameraPosition.position;
            }
            else
            {
                // 기본 1인칭 위치 (플레이어 머리 위치)
                Vector3 firstPersonOffset = new Vector3(0f, 1.6f, 0f);
                targetPosition = player.position + firstPersonOffset;
            }

            // 부드러운 위치 이동 (SmoothDamp 사용)
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref positionVelocity, 0.05f);

            // 카메라 회전 적용 (부드러운 회전)
            Quaternion targetCameraRotation = Quaternion.Euler(firstPersonVerticalAngle, firstPersonHorizontalAngle, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetCameraRotation, 20f * Time.deltaTime);
        }
        else
        {
            // 3인칭 모드
            if (IsFirstPersonMode)
            {
                IsFirstPersonMode = false;
                // 1인칭 수평 각도를 3인칭으로 이어받기
                currentHorizontalAngle = firstPersonHorizontalAngle;
                targetHorizontalAngle = firstPersonHorizontalAngle;

                // 수직 각도는 isometricAngleX로 부드럽게 복귀
                currentVerticalAngle = isometricAngleX;
                targetVerticalAngle = isometricAngleX;

                // Velocity 리셋
                horizontalAngleVelocity = 0f;
                verticalAngleVelocity = 0f;
                positionVelocity = Vector3.zero;

                // 마우스 커서 해제
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                Debug.Log("3인칭 모드로 전환");
            }

            // 마우스 오른쪽 버튼으로 카메라 회전 (자유 시점)
            if (enableMouseRotation && Input.GetMouseButton(1)) // 오른쪽 마우스 버튼
            {
                float mouseX = Input.GetAxis("Mouse X");
                float mouseY = Input.GetAxis("Mouse Y");

                // 수평 회전 (Y축)
                targetHorizontalAngle += mouseX * rotationSpeed;

                // 수직 회전 (X축)
                targetVerticalAngle -= mouseY * rotationSpeed;

                // 각도 제한
                targetVerticalAngle = Mathf.Clamp(targetVerticalAngle, minVerticalAngle, maxVerticalAngle);
            }

            // 각도 정규화 (0-360도 범위)
            targetHorizontalAngle = Mathf.Repeat(targetHorizontalAngle, 360f);

            // 부드러운 회전 전환 (SmoothDampAngle 사용)
            currentHorizontalAngle = Mathf.SmoothDampAngle(currentHorizontalAngle, targetHorizontalAngle, ref horizontalAngleVelocity, 1f / rotationSmoothness);
            currentVerticalAngle = Mathf.SmoothDampAngle(currentVerticalAngle, targetVerticalAngle, ref verticalAngleVelocity, 1f / rotationSmoothness);

            // 카메라 회전 적용 (수직, 수평 모두 적용)
            transform.rotation = Quaternion.Euler(currentVerticalAngle, currentHorizontalAngle, 0f);

            // 오프셋 계산 (현재 거리와 회전 기반)
            Vector3 offset = CalculateOffset();

            // 목표 위치 계산 (플레이어 중앙)
            Vector3 desiredPosition = player.position + offset;

            // 부드러운 이동 (SmoothDamp 사용으로 더 자연스러운 감속/가속)
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref positionVelocity, 1f / smoothSpeed);
        }
    }

    /// <summary>
    /// 현재 회전과 거리를 고려한 오프셋 계산
    /// </summary>
    private Vector3 CalculateOffset()
    {
        // 수직, 수평 각도 모두 사용하여 회전 계산
        Quaternion rotation = Quaternion.Euler(currentVerticalAngle, currentHorizontalAngle, 0f);
        Vector3 direction = rotation * Vector3.back; // 카메라가 뒤에서 보도록

        return direction * currentDistance;
    }
}
