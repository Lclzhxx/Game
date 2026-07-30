// =============================================================
// 文件：CameraRig.cs
// 作用：把主相机锁成"斜 45° 微俯、低 FOV(28°)、固定机位"。
//       玩家移动时相机【只平移跟随、绝不旋转】，用 2.5D 作假。
// 挂到：场景里的 Main Camera（GreyboxBuilder 会自动给它挂上并设好 target）。
// Inspector 设置：
//   - target：拖入玩家物体（Builder 自动设好）
//   - offset：相机相对玩家的固定偏移（世界坐标），默认(0,14,14)即45°俯视
//   - useSmoothing：是否平滑跟随
// 无需设置：旋转角度在 Awake 里一次性算好，之后永不改（破坏 2.5D 就在此）。
// 注意：需在 Unity 2022.3 下编译（经典 MonoBehaviour API，无版本风险）。
// =============================================================

using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraRig : MonoBehaviour
{
    [Header("跟随目标（拖入玩家 Transform）")]
    public Transform target;

    [Header("相机相对玩家的固定偏移（世界坐标）")]
    public Vector3 offset = new Vector3(0f, 14f, 14f); // y==z => 正好 45° 俯视

    [Header("平滑跟随")]
    public bool useSmoothing = true;
    public float smoothTime = 0.15f;

    private Camera m_Cam;
    private Vector3 m_CurrentVel;

    void Awake()
    {
        m_Cam = GetComponent<Camera>();
        m_Cam.orthographic = false;   // 透视相机
        m_Cam.fieldOfView = 28f;     // 低 FOV = 长焦，强化 2.5D 空间压缩

        // 固定斜 45° 微俯：角度完全由 offset 决定，只在 Awake 设一次。
        if (target != null)
        {
            transform.position = target.position + offset;
            // 看向 -offset 方向（即正对玩家），之后不再改动 rotation。
            transform.rotation = Quaternion.LookRotation(-offset.normalized, Vector3.up);
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 只做平移跟随：相机 = 玩家 + 固定偏移。
        // 因为偏移恒定，相机到玩家的方向恒定 => 固定机位始终正对玩家，但世界视角不旋转。
        Vector3 desired = target.position + offset;
        if (useSmoothing)
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref m_CurrentVel, smoothTime);
        else
            transform.position = desired;

        // 关键：本脚本【绝不修改 rotation】，否则会破坏 2.5D 作假。
    }
}
