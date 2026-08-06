// =============================================================
// 文件：PlayerCombatController.cs
// 作用：灰盒玩家控制（战斗骨架 S3 · E3-S1 重写）。
//       有限状态机：Idle / Move / Dodge / Attack，确定性、基于帧（无协程，便于 EditMode 单测稳定）。
//       FSM 纯逻辑抽离到 PlayerCombatStateMachine（零 UnityEngine 依赖，可 EditMode 测试）：
//       每帧 MonoBehaviour 外壳读取【经典 Input】构建 InputSnapshot -> 调 stateMachine.Tick(dt, snapshot) -> 应用到 CharacterController。
//       复用灰盒手感：相机相对 XZ 移动、空格闪避翻滚（带无敌帧 + 冷却）、鼠标左键普攻（OverlapSphere 命中 DummyEnemy）。
// 经典 Input：使用 UnityEngine.Input / Input.GetAxisRaw / Input.GetKeyDown / Input.GetMouseButtonDown。
//       新 Input System 迁移（Q5）已推迟到 E0-S4，此处不引入，不改动 ProjectSettings.activeInputHandler。
// 挂到：玩家物体（GreyboxBuilder.CreatePlayer 自动 AddComponent，含 CharacterController）。
// Inspector：移动 / 闪避(速度·时长·冷却·无敌) / 普攻(范围·伤害·冷却) / showDebug，默认值与原 PlayerController 对齐。
// 注意：需在 Unity 2022.3（经典 API / CharacterController）下编译。
// =============================================================

using UnityEngine;
using System;

// 战斗状态（与纯逻辑类共用，零 UnityEngine 依赖）
public enum CombatState
{
    Idle,
    Move,
    Dodge,
    Attack
}

// 数据驱动战斗参数（纯数据，供外壳从序列化字段构建后注入 FSM）
public struct CombatStats
{
    public float moveSpeed;
    public float dodgeSpeed;
    public float dodgeDuration;
    public float dodgeCooldown;
    public float dodgeInvincibleTime;
    public float attackRange;
    public float attackDamage;
    public float attackCooldown;
}

// 单帧输入快照（纯数据，外壳每帧从经典 Input 构建，不含任何 UnityEngine 类型）
public struct InputSnapshot
{
    public float moveX;          // 已归一化的相机相对移动方向 X
    public float moveZ;          // 已归一化的相机相对移动方向 Z
    public float facingX;        // 无移动时的回退朝向 X（外壳提供 flatten 后的 transform.forward）
    public float facingZ;        // 无移动时的回退朝向 Z
    public bool dodgePressed;    // 空格本帧按下（边沿）
    public bool attackPressed;   // 鼠标左键本帧按下（边沿）
}

[RequireComponent(typeof(CharacterController))]
public class PlayerCombatController : MonoBehaviour
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
    private PlayerCombatStateMachine m_FSM;
    private Vector3 m_Velocity;

    // 暴露无敌帧（保留原 public getter，供外部/调试读取）
    public bool IsInvincible { get { return m_FSM != null && m_FSM.IsInvincible; } }

    // 便于调试/外部读取当前状态
    public CombatState CurrentState { get { return (m_FSM != null) ? m_FSM.State : CombatState.Idle; } }

    void Awake()
    {
        m_CC = GetComponent<CharacterController>();
        m_Cam = Camera.main;

        // 数据驱动：从序列化字段构建 CombatStats 注入纯 FSM
        CombatStats s = new CombatStats
        {
            moveSpeed = moveSpeed,
            dodgeSpeed = dodgeSpeed,
            dodgeDuration = dodgeDuration,
            dodgeCooldown = dodgeCooldown,
            dodgeInvincibleTime = dodgeInvincibleTime,
            attackRange = attackRange,
            attackDamage = attackDamage,
            attackCooldown = attackCooldown
        };
        m_FSM = new PlayerCombatStateMachine(s);
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // ---- 经典 Input（New Input 推迟到 E0-S4，不引入）----
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        bool dodgePressed = Input.GetKeyDown(KeyCode.Space);
        bool attackPressed = Input.GetMouseButtonDown(0);

        // 相机相对 XZ 移动（W=远离相机 / S=靠近 / A=屏幕左 / D=屏幕右）
        Transform camT = (m_Cam != null) ? m_Cam.transform : (Camera.main != null ? Camera.main.transform : null);
        Vector3 forward = Vector3.forward, right = Vector3.right;
        if (camT != null)
        {
            forward = camT.forward; forward.y = 0f; forward.Normalize();
            right = camT.right; right.y = 0f; right.Normalize();
        }
        Vector3 move = (forward * v + right * h);
        if (move.sqrMagnitude > 1f) move.Normalize();

        // 无移动时朝向回退为当前 facing（flatten）
        Vector3 facing = (move.sqrMagnitude > 0.0001f) ? move : transform.forward;
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.0001f) facing.Normalize();

        // 构快照（纯数据，喂给 FSM）
        InputSnapshot snap = new InputSnapshot
        {
            moveX = move.x, moveZ = move.z,
            facingX = facing.x, facingZ = facing.z,
            dodgePressed = dodgePressed, attackPressed = attackPressed
        };

        // FSM 推进（确定性、基于注入 dt）
        m_FSM.Tick(dt, snap);

        // ---- 应用移动 ----
        if (m_FSM.State == CombatState.Dodge)
        {
            Vector3 ddir = new Vector3(m_FSM.DodgeDirX, 0f, m_FSM.DodgeDirZ);
            m_Velocity = ddir * dodgeSpeed;
        }
        else
        {
            m_Velocity = move * moveSpeed;
        }
        // 轻微向下速度，贴住平地（灰盒地面在 y=0）
        m_Velocity.y = -8f;
        m_CC.Move(m_Velocity * dt);

        // 面向移动方向（保留灰盒：闪避中不转向）
        if (m_FSM.State != CombatState.Dodge && move.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(move);

        // 普攻：消费本帧触发标志（OverlapSphere 命中 DummyEnemy）
        if (m_FSM.AttackTriggeredThisTick) DoAttack();
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

// =============================================================
// 纯逻辑战斗状态机（ZERO UnityEngine 依赖）
//   - 不继承 MonoBehaviour，不引用 Time.deltaTime，不引用 Input。
//   - 所有推进由外部注入 dt + InputSnapshot 驱动 -> EditMode 可确定性单测。
//   - 仅用 System.MathF（BCL），不触碰任何 UnityEngine API。
//   外壳（PlayerCombatController）负责读取经典 Input、应用移动/动作到 CharacterController。
// =============================================================
public class PlayerCombatStateMachine
{
    private CombatStats m_Stats;

    private CombatState m_State = CombatState.Idle;
    private float m_DodgeTimer = 0f;       // 当前闪避剩余时长
    private float m_DodgeCd = 0f;          // 闪避冷却剩余
    private float m_Invincible = 0f;       // 无敌帧剩余
    private float m_AttackCd = 0f;         // 普攻冷却剩余
    private float m_DodgeDirX = 0f;        // 锁定的闪避方向
    private float m_DodgeDirZ = 1f;
    private bool m_AttackTriggered = false;

    // ---- 对外只读状态/计时器 ----
    public CombatState State { get { return m_State; } }
    public float DodgeTimerRemaining { get { return MathF.Max(0f, m_DodgeTimer); } }
    public float DodgeCooldownRemaining { get { return MathF.Max(0f, m_DodgeCd); } }
    public float InvincibleRemaining { get { return MathF.Max(0f, m_Invincible); } }
    public float AttackCooldownRemaining { get { return MathF.Max(0f, m_AttackCd); } }
    public float DodgeDirX { get { return m_DodgeDirX; } }
    public float DodgeDirZ { get { return m_DodgeDirZ; } }
    public bool AttackTriggeredThisTick { get { return m_AttackTriggered; } }
    public bool IsInvincible { get { return m_Invincible > 0f; } }

    public PlayerCombatStateMachine(CombatStats stats)
    {
        m_Stats = stats;
    }

    // 运行时可更新数据驱动参数（如 Inspector 改动后刷新）
    public void SetStats(CombatStats stats) { m_Stats = stats; }

    // 复位到初始状态（重生/测试用）
    public void Reset()
    {
        m_State = CombatState.Idle;
        m_DodgeTimer = 0f;
        m_DodgeCd = 0f;
        m_Invincible = 0f;
        m_AttackCd = 0f;
        m_DodgeDirX = 0f;
        m_DodgeDirZ = 1f;
        m_AttackTriggered = false;
    }

    // 确定性推进：dt 与输入均由外部注入（无 Time.deltaTime / Input）
    public void Tick(float dt, InputSnapshot input)
    {
        if (dt < 0f) dt = 0f;
        m_AttackTriggered = false;

        // 计时器衰减
        m_DodgeCd = MathF.Max(0f, m_DodgeCd - dt);
        m_Invincible = MathF.Max(0f, m_Invincible - dt);
        m_AttackCd = MathF.Max(0f, m_AttackCd - dt);

        float moveSqr = input.moveX * input.moveX + input.moveZ * input.moveZ;

        // 上一帧 Attack 态本帧结算回 Idle/Move（Attack 为瞬时态，不锁移动，保留灰盒手感）
        if (m_State == CombatState.Attack)
            m_State = (moveSqr > 0.0001f) ? CombatState.Move : CombatState.Idle;

        // 普攻触发（边沿；Dodge 中不触发；冷却就绪才触发）
        if (input.attackPressed && m_AttackCd <= 0f && m_State != CombatState.Dodge)
        {
            m_AttackCd = m_Stats.attackCooldown;
            m_AttackTriggered = true;
            m_State = CombatState.Attack; // 持续到下一帧结算
        }

        // 闪避触发（空格；冷却就绪才触发；Dodge 中不重复触发 -> 闪避优先于普攻）
        if (input.dodgePressed && m_State != CombatState.Dodge && m_DodgeCd <= 0f)
        {
            m_State = CombatState.Dodge;
            m_DodgeTimer = m_Stats.dodgeDuration;
            m_DodgeCd = m_Stats.dodgeCooldown;
            m_Invincible = m_Stats.dodgeInvincibleTime;
            // 锁定闪避方向：有移动用移动方向，否则用朝向（外壳提供）
            if (moveSqr > 0.0001f)
            {
                float inv = 1f / MathF.Sqrt(moveSqr);
                m_DodgeDirX = input.moveX * inv;
                m_DodgeDirZ = input.moveZ * inv;
            }
            else
            {
                m_DodgeDirX = input.facingX;
                m_DodgeDirZ = input.facingZ;
            }
        }

        // 闪避进行 / 结束
        if (m_State == CombatState.Dodge)
        {
            m_DodgeTimer -= dt;
            if (m_DodgeTimer <= 0f)
                m_State = (moveSqr > 0.0001f) ? CombatState.Move : CombatState.Idle;
        }
        else if (m_State == CombatState.Idle)
        {
            if (moveSqr > 0.0001f) m_State = CombatState.Move;
        }
        else if (m_State == CombatState.Move)
        {
            if (moveSqr <= 0.0001f) m_State = CombatState.Idle;
        }
    }
}
