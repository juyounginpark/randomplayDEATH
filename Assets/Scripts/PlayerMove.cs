using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("플레이어 이동 속도")]
    public float moveSpeed = 5f;

    [Tooltip("고정할 Y 좌표")]
    public float fixedYPosition = 0f;

    [Tooltip("회전 속도")]
    [Range(5f, 20f)]
    public float rotationSpeed = 15f;

    [Header("Collision Settings")]
    [Tooltip("벽 감지 거리")]
    public float wallCheckDistance = 0.1f;

    [Tooltip("충돌 레이어")]
    public LayerMask collisionMask = -1;

    [Header("Camera Settings")]
    [Tooltip("카메라 참조 (자동으로 Main Camera를 찾음)")]
    public Camera mainCamera;

    private Rigidbody rb;
    private Vector3 moveDirection;
    private CameraMove cameraMove;

    // 캐싱
    private Vector3 cachedCameraForward;
    private Vector3 cachedCameraRight;

    void Start()
    {
        // Rigidbody 컴포넌트 가져오기 (있으면)
        rb = GetComponent<Rigidbody>();

        // 시작 시 현재 Y 위치를 고정 위치로 설정
        fixedYPosition = transform.position.y;

        // Rigidbody가 있으면 설정
        if (rb != null)
        {
            // 모든 회전 고정 (밀려나고 회전하는 문제 방지) 및 Y축 이동 고정
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

            // 물리 설정 (충돌 안정성)
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // 질량 및 저항 설정
            rb.mass = 1f;
            rb.linearDamping = 10f;
            rb.angularDamping = 999f; // 회전 저항 극대화

            // 중력 사용 여부
            rb.useGravity = false;

            Debug.Log("Rigidbody 설정 완료: 회전 완전 고정, 중력 OFF");
        }
        else
        {
            Debug.LogWarning("Rigidbody가 없습니다. 물리 충돌이 제대로 작동하지 않을 수 있습니다.");
        }

        // 카메라 자동 찾기
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("Main Camera를 찾을 수 없습니다. 카메라 기준 이동이 작동하지 않을 수 있습니다.");
            }
        }

        // CameraMove 컴포넌트 가져오기
        if (mainCamera != null)
        {
            cameraMove = mainCamera.GetComponent<CameraMove>();
            if (cameraMove == null)
            {
                Debug.LogWarning("CameraMove 컴포넌트를 찾을 수 없습니다. 1인칭 모드가 제대로 작동하지 않을 수 있습니다.");
            }
        }

        // Collider 설정 확인 및 조정
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            // Physics Material 생성 (마찰 제거)
            PhysicsMaterial physicMat = new PhysicsMaterial("PlayerPhysics");
            physicMat.dynamicFriction = 0f;
            physicMat.staticFriction = 0f;
            physicMat.bounciness = 0f;
            physicMat.frictionCombine = PhysicsMaterialCombine.Minimum;
            physicMat.bounceCombine = PhysicsMaterialCombine.Minimum;

            col.material = physicMat;

            Debug.Log($"Collider 설정: {col.GetType().Name}, Physics Material 적용");
        }
        else
        {
            Debug.LogWarning("Collider가 없습니다! Capsule Collider를 추가하세요.");
        }
    }

    void Update()
    {
        // WASD 입력 받기
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // 입력이 없으면 이동 방향 0으로 설정
        if (Mathf.Abs(horizontal) < 0.01f && Mathf.Abs(vertical) < 0.01f)
        {
            moveDirection = Vector3.zero;
            return;
        }

        // 카메라 방향 벡터 캐싱 (최적화)
        if (mainCamera != null)
        {
            cachedCameraForward = mainCamera.transform.forward;
            cachedCameraRight = mainCamera.transform.right;

            // Y 컴포넌트를 0으로 만들어 XZ 평면에 투영
            cachedCameraForward.y = 0f;
            cachedCameraRight.y = 0f;

            // 정규화
            cachedCameraForward.Normalize();
            cachedCameraRight.Normalize();

            // 카메라 기준 이동 방향 계산
            moveDirection = cachedCameraForward * vertical + cachedCameraRight * horizontal;

            // 정규화 (대각선 이동 속도 보정)
            if (moveDirection.sqrMagnitude > 0.01f)
            {
                moveDirection.Normalize();
            }
        }
        else
        {
            // 카메라가 없으면 기본 월드 좌표계 사용
            moveDirection = new Vector3(horizontal, 0f, vertical);
            if (moveDirection.sqrMagnitude > 0.01f)
            {
                moveDirection.Normalize();
            }
        }
    }

    void LateUpdate()
    {
        // FPS 모드에서 회전 강제 고정 (마지막 확인)
        bool isFirstPerson = cameraMove != null && cameraMove.IsFirstPersonMode;
        if (isFirstPerson && firstPersonLockedRotation != Quaternion.identity)
        {
            transform.rotation = firstPersonLockedRotation;
        }
    }


    private Quaternion firstPersonLockedRotation;
    private bool wasFirstPerson = false;

    void FixedUpdate()
    {
        // 1인칭 모드 확인
        bool isFirstPerson = cameraMove != null && cameraMove.IsFirstPersonMode;

        // 모드 전환 감지
        if (isFirstPerson != wasFirstPerson)
        {
            if (isFirstPerson)
            {
                // 1인칭 모드 진입: 현재 회전 잠금
                firstPersonLockedRotation = transform.rotation;
                Debug.Log($"FPS 모드 진입 - 회전 잠금: {firstPersonLockedRotation.eulerAngles}");
            }
            else
            {
                // 3인칭 모드 복귀
                firstPersonLockedRotation = Quaternion.identity;
                Debug.Log("3인칭 모드 복귀");
            }
            wasFirstPerson = isFirstPerson;
        }

        // === FPS 모드 전용 로직 ===
        if (isFirstPerson)
        {
            HandleFirstPersonMovement();
            return;
        }

        // === 3인칭 모드 전용 로직 ===
        HandleThirdPersonMovement();
    }

    /// <summary>
    /// FPS 모드 이동 처리 - 회전 없이 좌표만 이동
    /// </summary>
    private void HandleFirstPersonMovement()
    {
        // 매 프레임 회전 강제 고정 (물리 엔진의 회전도 차단)
        if (rb != null)
        {
            rb.MoveRotation(firstPersonLockedRotation);
        }
        transform.rotation = firstPersonLockedRotation;

        // 이동 방향이 없으면 리턴
        if (moveDirection.sqrMagnitude < 0.0001f)
            return;

        // 카메라 시점 기준 좌표 이동만 수행
        Vector3 movement = moveDirection * moveSpeed * Time.fixedDeltaTime;

        if (rb != null)
        {
            Vector3 newPosition = rb.position + movement;
            newPosition.y = fixedYPosition;
            rb.MovePosition(newPosition);
            // 이동 후에도 회전 다시 고정
            rb.MoveRotation(firstPersonLockedRotation);
        }
        else
        {
            Vector3 newPosition = transform.position + movement;
            newPosition.y = fixedYPosition;
            transform.position = newPosition;
            transform.rotation = firstPersonLockedRotation;
        }
    }

    /// <summary>
    /// 3인칭 모드 이동 처리 - 이동 방향으로 회전
    /// </summary>
    private void HandleThirdPersonMovement()
    {
        // 이동 방향이 없으면 리턴
        if (moveDirection.sqrMagnitude < 0.0001f)
            return;

        // 이동 계산
        Vector3 movement = moveDirection * moveSpeed * Time.fixedDeltaTime;

        // Rigidbody가 있으면 물리 기반 이동
        if (rb != null)
        {
            Vector3 newPosition = rb.position + movement;
            newPosition.y = fixedYPosition;
            rb.MovePosition(newPosition);
        }
        else
        {
            // Rigidbody가 없으면 Transform 기반 이동
            Vector3 newPosition = transform.position + movement;
            newPosition.y = fixedYPosition;
            transform.position = newPosition;
        }

        // 이동 방향으로 부드럽게 회전
        Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
    }

}
