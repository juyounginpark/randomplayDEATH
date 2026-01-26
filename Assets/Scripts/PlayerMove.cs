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

    // 충돌 방지용
    private Vector3 lastValidPosition;

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
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;

            // 중력 사용 여부
            rb.useGravity = false;

            Debug.Log("Rigidbody 설정 완료: 회전 고정, 중력 OFF");
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

    // Knockback Settings
    [Header("Knockback Settings")]
    [Tooltip("충돌 시 튕겨나가는 힘")]
    public float knockbackForce = 5f;
    [Tooltip("튕겨나가는 시간")]
    public float knockbackDuration = 0.5f;
    [Tooltip("충돌 감지 태그")]
    public string collisionTag = "Map";

    private bool isKnockback = false;

    void FixedUpdate()
    {
        // 넉백 중이면 이동 입력 무시 (넉백 루틴에서 이동 처리)
        if (isKnockback) return;

        // 이동 방향이 없으면 리턴
        if (moveDirection.sqrMagnitude < 0.0001f)
            return;

        // 1인칭 모드 확인
        bool isFirstPerson = cameraMove != null && cameraMove.IsFirstPersonMode;

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

        // 3인칭 모드에서만 이동 방향으로 회전
        // 1인칭 모드에서는 CameraMove에서 플레이어 회전 처리
        if (!isFirstPerson)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 맵과 충돌 시 넉백
        if (collision.gameObject.CompareTag(collisionTag) && !isKnockback)
        {
            StartCoroutine(KnockbackRoutine(collision.contacts[0].normal));
        }
    }

    private System.Collections.IEnumerator KnockbackRoutine(Vector3 hitNormal)
    {
        isKnockback = true;

        float timer = 0f;
        // 충돌 반대 방향 (뒤로) = 법선 벡터 방향 (보통 벽의 normal은 튀어나오는 방향이므로 플레이어에게는 뒤로 가는 방향)
        // 혹은 단순히 플레이어의 -forward를 쓸 수도 있지만, 충돌 각도에 따라 튕기는게 더 자연스러울 수 있음.
        // 여기서는 "쭉 뒤로 가"라는 요청에 맞춰 현재 보는 방향의 반대로 설정하거나, 충돌 면의 반대로 설정.
        // "쭉 뒤로" -> 플레이어의 진행 반대 방향이 가장 직관적일 수 있음.
        // 하지만 벽에 비스듬히 부딪혔을때 미끄러지는게 아니라 튕겨야 한다면 Normal 활용.
        // 요청: "플레이어가 쭉 뒤로 가" -> 단순히 -transform.forward 사용 시 회전이 꼬일 수 있음.
        // 가장 안전한 건 충돌 지점의 Normal 벡터 방향으로 밀어내는 것.
        
        Vector3 knockbackDir = hitNormal;
        knockbackDir.y = 0; // Y축 이동 방지
        knockbackDir.Normalize();

        while (timer < knockbackDuration)
        {
            timer += Time.fixedDeltaTime;

            Vector3 movement = knockbackDir * knockbackForce * Time.fixedDeltaTime;

            if (rb != null)
            {
                Vector3 newPosition = rb.position + movement;
                newPosition.y = fixedYPosition;
                rb.MovePosition(newPosition);
            }
            else
            {
                Vector3 newPosition = transform.position + movement;
                newPosition.y = fixedYPosition;
                transform.position = newPosition;
            }

            yield return new WaitForFixedUpdate();
        }

        isKnockback = false;
    }
}
