using System.Collections;
using Unity.Android.Gradle.Manifest;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.Text;

public abstract class Character : NetworkBehaviour, ICharacter
{
    public CinemachineFreeLookModifier cam;

    bool isGrounded;

    CharacterController controller;
    Vector3 velocity;

    protected Animator animator;
    protected NetworkAnimator networkAnimator;
    protected float maxHp = 100;
    protected float currentHp;
    protected float moveSpeed = 5;
    protected Quaternion currentRotation;




    public virtual void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        networkAnimator = GetComponent<NetworkAnimator>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false; 
    }

    public abstract void Attack(); // 공격 구현
    public abstract void Interaction(); // 상호작용 구현
    public abstract void HandleInput(); // 키 입력 구현
    public abstract void SetHP(); // 직업 별 체력적용

    // 이동 메서드
    public virtual void Move(CinemachineCamera cam, Rigidbody rb)
    {
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        float scaleFactor = transform.localScale.y;
        float adjustedMoveSpeed = moveSpeed * scaleFactor;

        // 카메라 방향에 따른 이동
        Vector3 moveDirection = cam.gameObject.transform.right * moveX + cam.gameObject.transform.forward * moveZ;
        moveDirection.y = 0;

        Vector3 velocity = moveDirection.normalized * adjustedMoveSpeed;
        velocity.y = rb.linearVelocity.y;

        rb.linearVelocity = velocity;

        // 애니메이션 적용
        float speed = moveDirection.magnitude > 0.1f ? 1f : 0f;
        HandleAnimationserverRpc("Move", speed, 0.1f, Time.deltaTime);

        // 일정 움직임이 있을때만 회전값 변경
        if (moveDirection.magnitude > 0.1f)
        {
            currentRotation = Quaternion.LookRotation(moveDirection);
        }

        // 회전 적용 (회전 값은 계속 유지됨)
        rb.rotation = Quaternion.Normalize(Quaternion.Slerp(rb.rotation, currentRotation, Time.deltaTime * 10f));
    }

    [ServerRpc]
    void HandleAnimationserverRpc(string name, float value, float dampTime, float deltaTime)
    {
        networkAnimator.Animator.SetFloat(name, value, dampTime, deltaTime);
    }

    // 피격 메서드
    public virtual void TakeDamage(float damage)
    {
        currentHp -= damage;
        if (currentHp < 0)
        {
            Die();
        }
    }

    // 사망 메서드
    public void Die()
    {
        Debug.Log("Die");
    }

    void AssignCamera()
    {
        cam = FindFirstObjectByType<CinemachineFreeLookModifier>();

        if (cam != null)
        {
            cam.transform.position = transform.position;
            cam.gameObject.GetComponent<CinemachineCamera>().Follow = transform;
            cam.gameObject.GetComponent<CinemachineCamera>().LookAt = transform;
        }
        else
        {
            Debug.LogError("Cinemachine Camera를 찾을 수 없습니다.");
        }
    }

    IEnumerator CamRoutine()
    {
        yield return new WaitUntil(() => FindFirstObjectByType<CinemachineCamera>() != null);
        AssignCamera();
    }

}
