// =============================================================
// 文件：PlayerController.cs
// 作用：灰盒玩家控制。胶囊体 + WASD 移动（锁定 XZ 平面、Y 锁地，模拟 2.5D 走位）
//       + 空格闪避翻滚（带短无敌帧 + 冷却，验证"撤退遁术"手感雏形）
//       + 鼠标左键普攻（对范围内敌人造成伤害标记，使敌人闪红）。
// 挂到：玩家物体（GreyboxBuilder 会自动创建并挂好，含 CharacterController）。
// Inspector 设置：移动速度 / 闪避速度·时长·冷却·无敌时长 / 普攻范围·伤害·冷却 / showDebug。
// 无需设置：碰撞用 CharacterController（比 Rigidbody 更不易卡 bug），已在 Builder 里配好。
// 注意：需在 Unity 2022.3 下编译（经典 Input / CharacterController API）。
// =============================================================

using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("移动")]
    public float moveSpeed = 5f;

    [Header("闪避翻滚（空格）")]
    public float dodgeSpeed = 14f;
    public float dodgeDuration = 0.25f;
    public float dodgeCooldown = 0.8f;
    public float dodgeInvincibleTime = 0.3f;

    [Header("普攻（鼠标左键）")]
    public float attackRange = 2.5f;
    public float attackDamage = 34f;
    public float attackCooldown = 0.4f;

    [Header("调试")]
    public bool showDebug = true;

    private CharacterController m_CC;
    private Camera m_Cam;
    private Vector3 m_Velocity;
    private bool m_IsDodging = false;
    private float m_DodgeTimer = 0f;
    private Vector3 m_DodgeDir = Vector3.forward;
    private float m_DodgeCdTimer = 0f;
    private float m_InvincibleTimer = 0f;
    private float m_AttackCdTimer = 0f;

    public bool IsInvincible { get { return m_InvincibleTimer > 0f; } }

    void Awake()
    {
        m_CC = GetComponent<CharacterController>();
        m_Cam = Camera.main;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        m_DodgeCdTimer   = Mathf.Max(0f, m_DodgeCdTimer - dt);
        m_InvincibleTimer = Mathf.Max(0f, m_InvincibleTimer - dt);
        m_AttackCdTimer   = Mathf.Max(0f, m_AttackCdTimer - dt);

        // 输入（XZ 平面，相机相对：W=远离相机 / S=靠近 / A=屏幕左 / D=屏幕右）
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Transform camT = (m_Cam != null) ? m_Cam.transform : (Camera.main != null ? Camera.main.transform : null);
        Vector3 forward = Vector3.forward, right = Vector3.right;
        if (camT != null)
        {
            forward = camT.forward; forward.y = 0f; forward.Normalize();
            right = camT.right; right.y = 0f; right.Normalize();
        }
        Vector3 move = (forward * v + right * h);
        if (move.sqrMagnitude > 1f) move.Normalize();

        // 触发闪避翻滚
        if (Input.GetKeyDown(KeyCode.Space) && !m_IsDodging && m_DodgeCdTimer <= 0f)
        {
            m_IsDodging = true;
            m_DodgeTimer = dodgeDuration;
            m_DodgeCdTimer = dodgeCooldown;
            m_InvincibleTimer = dodgeInvincibleTime;
            m_DodgeDir = (move.sqrMagnitude > 0.01f) ? move : transform.forward;
            m_DodgeDir.Normalize();
        }

        if (m_IsDodging)
        {
            m_DodgeTimer -= dt;
            m_Velocity = m_DodgeDir * dodgeSpeed;
            if (m_DodgeTimer <= 0f) m_IsDodging = false;
        }
        else
        {
            m_Velocity = move * moveSpeed;
        }

        // 轻微向下速度，贴住平地（灰盒地面在 y=0）
        m_Velocity.y = -8f;

        m_CC.Move(m_Velocity * dt);

        // 面向移动方向
        if (move.sqrMagnitude > 0.01f && !m_IsDodging)
            transform.rotation = Quaternion.LookRotation(move);

        // 普攻
        if (Input.GetMouseButtonDown(0) && m_AttackCdTimer <= 0f)
        {
            m_AttackCdTimer = attackCooldown;
            DoAttack();
        }
    }

    // 对范围内敌人造成伤害标记（灰盒：敌人闪红即可）
    void DoAttack()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, attackRange);
        foreach (var c in hits)
        {
            DummyEnemy e = c.GetComponent<DummyEnemy>();
            if (e != null) e.TakeHit(attackDamage);
        }
        if (showDebug) Debug.Log("[Player] 普攻命中检测，范围=" + attackRange);
    }
}
