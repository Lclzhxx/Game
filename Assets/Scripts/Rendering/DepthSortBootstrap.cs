// =============================================================
// 文件：DepthSortBootstrap.cs（E2-S2，ADR-009）
// 作用：把主相机的透明队列排序切到 CustomAxis（Y-Z 合成轴），
//       解决斜 45° 下透明面片前后站位穿插错排（C4/R4）。
// 挂到：主相机（CameraRig 旁）。GreyboxBuilder 会自动挂载（用户零配置）。
// 红线（验收）：零每帧成本——只在 Awake 一次性执行，本脚本【禁止】出现
//   Update/LateUpdate/FixedUpdate（PlayMode 测试用反射断言防回归）。
// 轴推导（单一事实来源 = CameraRig.offset，不写死轴值）：
//   axis = -offset.normalized
//   offset=(0,14,14)（恰 45°）→ axis ≈ (0, -0.7071, -0.7071)。
// 符号推导（对齐 Unity CustomAxis 语义：沿轴投影值大者视为更远、先绘制）：
//   相机在 +Y+Z 高处俯视 -Y-Z 方向 ⇒ 离相机更远的物体 y+z 更小
//   ⇒ 想让"更远者先画"，轴必须取 (0,-1,-1) 方向（= 相机前向 = -offset.normalized）。
//   （若取 +(0,1,1) 则前后反转：近者先画、混合次序全错。）
// 适用范围：只影响 Transparent 队列；不透明物一律走深度缓冲（控制清单：
//   禁止为排序把不透明材质改 Transparent；多面片组合体必挂 SortingGroup）。
// 注意：需在 Unity 2022.3 下编译（经典 Camera API，无版本风险）。
// =============================================================

using UnityEngine;

[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class DepthSortBootstrap : MonoBehaviour
{
    void Awake()
    {
        Apply(GetComponent<Camera>());
        // 一次性配置完毕。之后 offset 永不变（C3：固定机位禁旋转），无需任何每帧逻辑。
    }

    /// <summary>一次性把相机置为 CustomAxis 排序；轴从 CameraRig.offset 推导。</summary>
    public static void Apply(Camera cam)
    {
        if (cam == null) return;

        Vector3 offset = new Vector3(0f, 14f, 14f); // 与 CameraRig 默认值一致的兜底
        CameraRig rig = cam.GetComponent<CameraRig>();
        if (rig != null && rig.offset.sqrMagnitude > 1e-6f)
            offset = rig.offset;
        else if (rig != null)
            Debug.LogWarning("[DepthSortBootstrap] CameraRig.offset 非法（近零向量），排序轴回退默认 (0,14,14) 推导。");

        cam.transparencySortMode = TransparencySortMode.CustomAxis;
        cam.transparencySortAxis = DeriveAxis(offset);
    }

    /// <summary>排序轴 = -offset.normalized（相机前向）。独立纯函数，便于测试与复用。</summary>
    public static Vector3 DeriveAxis(Vector3 cameraOffset)
    {
        return (-cameraOffset).normalized;
    }
}
