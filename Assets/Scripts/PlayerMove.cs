using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("플레이어 이동 속도")]
    public float moveSpeed = 5f;

    [Tooltip("회전 속도")]
    [Range(5f, 20f)]
    public float rotationSpeed = 15f;

    [Header("Jump Settings")]
    [Tooltip("점프 힘")]
    [Range(1f, 20f)]
    public float jumpForce = 7f;

    [Tooltip("중력 배율")]
    [Range(1f, 5f)]
    public float gravityMultiplier = 2f;

    [Tooltip("지면 체크 거리")]
    [Range(0.1f, 1f)]
    public float groundCheckDistance = 0.2f;

    [Tooltip("지면 레이어")]
    public LayerMask groundMask = -1;

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
    private Collider col;

    // 캐싱
    private Vector3 cachedCameraForward;
    private Vector3 cachedCameraRight;

    // 점프 관련
    private bool isGrounded;
    private bool jumpRequested;

    // 이동 부드러움
    [Header("Smoothness Settings")]
    [Tooltip("즉각 반응 (true: 키 누르면 즉시 이동/멈춤, false: 부드러운 가속)")]
    public bool instantResponse = true;

    [Tooltip("지면에서 이동 가속도 (instantResponse false일 때만 사용)")]
    [Range(5f, 50f)]
    public float groundAcceleration = 20f;

    // 플레이어 제어 잠금
    private bool isFrozen = false;

    void Start()
    {
        // Rigidbody 컴포넌트 가져오기 (있으면)
        rb = GetComponent<Rigidbody>();

        // Rigidbody가 있으면 설정
        if (rb != null)
        {
            // 회전만 고정 (Y축 이동은 점프를 위해 자유롭게)
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            // 물리 설정 (충돌 안정성 및 부드러운 움직임)
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // 질량 및 저항 설정
            rb.mass = 1f;
            rb.linearDamping = 0f; // 저항 제거 (부드러운 이동)
            rb.angularDamping = 999f; // 회전 저항 극대화

            // 중력 활성화 (점프를 위해 필요)
            rb.useGravity = true;

            Debug.Log("Rigidbody 설정 완료: 회전 고정, 중력 ON, 점프 가능");
        }
        else
        {
            Debug.LogWarning("Rigidbody가 없습니다. 점프 기능이 작동하지 않습니다!");
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
        }
        
        // 만약 Main Camera에서 못 찾았으면 전체 검색 (안전장치)
        if (cameraMove == null)
        {
            cameraMove = FindObjectOfType<CameraMove>();
            if (cameraMove == null)
            {
                Debug.LogWarning("CameraMove 컴포넌트를 찾을 수 없습니다. 1인칭 모드가 제대로 작동하지 않을 수 있습니다.");
            }
        }

        // Collider 설정 확인 및 조정
        col = GetComponent<Collider>();
        if (col != null)
        {
            // Physics Material 생성 (마찰 완전 제거로 떨림 방지)
            PhysicsMaterial physicMat = new PhysicsMaterial("PlayerPhysics");
            physicMat.dynamicFriction = 0f;
            physicMat.staticFriction = 0f;
            physicMat.bounciness = 0f;
            physicMat.frictionCombine = PhysicsMaterialCombine.Minimum;
            physicMat.bounceCombine = PhysicsMaterialCombine.Minimum;

            col.material = physicMat;

            Debug.Log($"Collider 설정: {col.GetType().Name}, Physics Material 적용 (마찰 없음)");
        }
        else
        {
            Debug.LogWarning("Collider가 없습니다! Capsule Collider를 추가하세요.");
        }

        // 고정된 프레임레이트 설정 확인 (떨림 방지)
        if (Time.fixedDeltaTime > 0.02f)
        {
            Debug.LogWarning($"FixedDeltaTime이 {Time.fixedDeltaTime}초입니다. 부드러운 움직임을 위해 0.02초(50Hz) 이하를 권장합니다.");
        }
    }

    void Update()
    {
        // Frozen 상태면 모든 입력 무시
        if (isFrozen)
        {
            moveDirection = Vector3.zero;
            jumpRequested = false;
            return;
        }

        // WASD 입력 받기
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // 입력이 없으면 이동 방향 0으로 설정
        if (Mathf.Abs(horizontal) < 0.01f && Mathf.Abs(vertical) < 0.01f)
        {
            moveDirection = Vector3.zero;
        }
        else
        {
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

        // 점프 입력 감지 (스페이스바)
        if (Input.GetButtonDown("Jump"))
        {
            jumpRequested = true;
        }
    }

    void FixedUpdate()
    {
        // Frozen 상태면 모든 물리 동작 정지
        if (isFrozen)
        {
            if (rb != null)
            {
                // 모든 속도를 0으로 설정 (공중에 있어도 즉시 정지)
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            return;
        }

        // 1인칭 모드 확인
        bool isFirstPerson = cameraMove != null && cameraMove.IsFirstPersonMode;

        // 지면 체크
        CheckGrounded();

        // 점프 처리
        if (jumpRequested && isGrounded && rb != null)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            jumpRequested = false;
            Debug.Log("점프!");
        }

        // 추가 중력 적용 (더 자연스러운 점프)
        if (rb != null && !isGrounded)
        {
            rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
        }

        // 수평 이동 처리
        if (rb != null)
        {
            Vector3 targetVelocity;

            if (instantResponse)
            {
                // 즉각 반응 모드: 키 누르면 즉시 이동, 놓으면 즉시 정지
                if (moveDirection.sqrMagnitude > 0.0001f)
                {
                    // 이동 입력이 있을 때: 즉시 목표 속도로 이동
                    Vector3 targetHorizontalVelocity = moveDirection * moveSpeed;
                    targetVelocity = new Vector3(targetHorizontalVelocity.x, rb.linearVelocity.y, targetHorizontalVelocity.z);
                }
                else
                {
                    // 이동 입력이 없을 때: 즉시 정지 (Y축만 유지)
                    if (isGrounded)
                    {
                        targetVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                    }
                    else
                    {
                        // 공중에서는 수평 속도 약간 유지 (너무 갑작스럽지 않게)
                        Vector3 currentHorizontal = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                        Vector3 dampenedHorizontal = currentHorizontal * 0.95f; // 공중 감속
                        targetVelocity = new Vector3(dampenedHorizontal.x, rb.linearVelocity.y, dampenedHorizontal.z);
                    }
                }
            }
            else
            {
                // 부드러운 가속 모드
                Vector3 currentHorizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

                if (moveDirection.sqrMagnitude > 0.0001f)
                {
                    // 이동 입력이 있을 때
                    Vector3 targetHorizontalVelocity = moveDirection * moveSpeed;

                    // 부드러운 가속 (지면에서는 빠르게, 공중에서는 느리게)
                    float acceleration = isGrounded ? groundAcceleration : groundAcceleration * 0.3f;
                    currentHorizontalVelocity = Vector3.MoveTowards(currentHorizontalVelocity, targetHorizontalVelocity, acceleration * Time.fixedDeltaTime);

                    targetVelocity = new Vector3(currentHorizontalVelocity.x, rb.linearVelocity.y, currentHorizontalVelocity.z);
                }
                else
                {
                    // 이동 입력이 없을 때
                    if (isGrounded)
                    {
                        // 지면에서는 빠르게 감속
                        currentHorizontalVelocity = Vector3.MoveTowards(currentHorizontalVelocity, Vector3.zero, groundAcceleration * 2f * Time.fixedDeltaTime);
                        targetVelocity = new Vector3(currentHorizontalVelocity.x, rb.linearVelocity.y, currentHorizontalVelocity.z);
                    }
                    else
                    {
                        // 공중에서는 수평 속도 유지
                        targetVelocity = rb.linearVelocity;
                    }
                }
            }

            // velocity 설정
            rb.linearVelocity = targetVelocity;

            // 3인칭 모드에서만 이동 방향으로 회전
            // 1인칭 모드에서는 CameraMove에서 플레이어 회전 처리
            if (!isFirstPerson && moveDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
            }
        }
        else
        {
            // Rigidbody가 없을 때는 Transform 기반 이동
            if (moveDirection.sqrMagnitude > 0.0001f)
            {
                Vector3 movement = moveDirection * moveSpeed * Time.fixedDeltaTime;
                transform.position += movement;

                if (!isFirstPerson)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
                }
            }
        }
    }

    /// <summary>
    /// 지면 체크 (Raycast 사용)
    /// </summary>
    private void CheckGrounded()
    {
        if (col == null)
        {
            isGrounded = false;
            return;
        }

        // Collider 하단 중심점 계산
        Vector3 origin = col.bounds.center;
        float rayDistance = col.bounds.extents.y + groundCheckDistance;

        // 아래로 Raycast
        isGrounded = Physics.Raycast(origin, Vector3.down, rayDistance, groundMask);

        // 디버그용 (Scene 뷰에서 확인 가능)
        Debug.DrawRay(origin, Vector3.down * rayDistance, isGrounded ? Color.green : Color.red);
    }

    /// <summary>
    /// 플레이어 동작 정지 (외부에서 호출)
    /// </summary>
    public void FreezePlayer()
    {
        isFrozen = true;
        moveDirection = Vector3.zero;
        jumpRequested = false;

        if (rb != null)
        {
            // 모든 속도를 즉시 0으로
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Debug.Log("[PlayerMove] 플레이어 Freeze - 모든 동작 정지");
    }

    /// <summary>
    /// 플레이어 동작 재개 (외부에서 호출)
    /// </summary>
    public void UnfreezePlayer()
    {
        isFrozen = false;
        Debug.Log("[PlayerMove] 플레이어 Unfreeze - 동작 재개");
    }

    /// <summary>
    /// 현재 Freeze 상태 확인
    /// </summary>
    public bool IsFrozen()
    {
        return isFrozen;
    }

}
