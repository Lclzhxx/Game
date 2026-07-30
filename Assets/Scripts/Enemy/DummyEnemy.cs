// =============================================================
// 文件：DummyEnemy.cs
// 作用：灰盒敌人（胶囊体）。两种 spawn 用于测 Z 分层可读性（H1）：
//       - 地面敌人：贴地（hoverHeight = 0，绿色）
//       - 空中敌人：浮空一定高度（isFlying = true，hoverHeight > 0，青色）
//       行为：默认待机（chasePlayer=false）；被普攻命中闪红。
// 挂到：敌人物体（GreyboxBuilder 自动创建并配好；也可手动建胶囊体后挂本脚本）。
// Inspector 设置：
//   - isFlying：是否空中（空中会悬浮并轻微上下浮动）
//   - hoverHeight：空中悬浮高度（地面敌人保持 0 即可）
//   - chasePlayer：是否缓慢追玩家（默认 false，专注验证可读性）
//   - moveSpeed / maxHealth：行为/战斗参数
// 注意：需在 Unity 2022.3 下编译（经典 API）。
// =============================================================

using UnityEngine;

public class DummyEnemy : MonoBehaviour
{
    [Header("类型与高度")]
    public bool isFlying = false;
    public float hoverHeight = 2.5f;

    [Header("行为")]
    public bool chasePlayer = false;
    public float moveSpeed = 1.2f;

    [Header("战斗")]
    public float maxHealth = 100f;

    private float m_Health;
    private float m_FlashTimer = 0f;
    private Renderer m_Renderer;
    private Color m_BaseColor;
    private Transform m_Player;

    void Awake()
    {
        m_Renderer = GetComponentInChildren<Renderer>();
        if (m_Renderer != null) m_BaseColor = m_Renderer.sharedMaterial.color;
        m_Health = maxHealth;
    }

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) m_Player = p.transform;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // 命中闪红
        if (m_FlashTimer > 0f && m_Renderer != null)
        {
            m_FlashTimer -= dt;
            m_Renderer.material.color = Color.Lerp(m_BaseColor, Color.red, Mathf.Clamp01(m_FlashTimer / 0.15f));
        }

        // 空中悬浮（轻微浮动）
        if (isFlying && float.IsFinite(hoverHeight))
        {
            float targetY = hoverHeight + Mathf.Sin(Time.time * 1.5f) * 0.2f;
            Vector3 pos = transform.position;
            pos.y = Mathf.Lerp(pos.y, targetY, 0.1f);
            transform.position = pos;
        }

        // 缓慢追玩家（XZ 平面）
        if (chasePlayer)
        {
            if (m_Player == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) m_Player = p.transform;
            }
            if (m_Player != null)
            {
                Vector3 dir = m_Player.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    dir.Normalize();
                    transform.position += dir * moveSpeed * dt;
                    transform.LookAt(new Vector3(m_Player.position.x, transform.position.y, m_Player.position.z));
                }
            }
        }
    }

    // 被普攻调用：扣血 + 闪红（灰盒不销毁，血量见底转灰，保持场景稳定）
    public void TakeHit(float damage)
    {
        m_Health -= damage;
        m_FlashTimer = 0.15f;
        if (m_Renderer != null) m_Renderer.material.color = Color.red;
        if (m_Health <= 0f && m_Renderer != null)
            m_Renderer.material.color = new Color(0.2f, 0.2f, 0.2f);
    }
}
