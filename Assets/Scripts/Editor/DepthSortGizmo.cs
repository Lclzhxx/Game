// =============================================================
// 文件：DepthSortGizmo.cs（E2-S3）
// 作用：编辑期 gizmo 可视化 Y-Z 排序轴（C3/C4 固定 45° + Y-Z 排序），
//       便于美术/策划在 Scene 视图核对斜 45° 透明排序。
// 轴 = DepthSortBootstrap.DeriveAxis(CameraRig.offset) = -offset.normalized
//   offset=(0,14,14) => 轴 ≈ (0, -0.7071, -0.7071)。
// 纯编辑器：放 Editor 文件夹，运行时程序集零引用，零运行时成本。
// 接线：[DrawGizmo] 自动装配（场景里任何 CameraRig / DepthSortBootstrap 都会画），
//      无需手动挂脚本、无需运行时组件。
// 注意：需在 Unity 2022.3 下编译（Editor 专用 API）。
// =============================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace MJ.Editor
{
    /// <summary>编辑期排序轴可视化。仅 Editor 编译，运行时零成本。</summary>
    public static class DepthSortGizmo
    {
        private const float AxisLength = 6f;                       // gizmo 轴长度（世界单位）
        private static readonly Color AxisColor = new Color(1f, 0.4f, 0.1f); // 暖橙，醒目

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.InSelectionHierarchy)]
        static void DrawCameraRigAxis(CameraRig rig, GizmoType gizmoType)
        {
            if (rig == null) return;
            DrawAxis(rig.transform.position, DepthSortBootstrap.DeriveAxis(rig.offset));
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.InSelectionHierarchy)]
        static void DrawBootstrapAxis(DepthSortBootstrap bootstrap, GizmoType gizmoType)
        {
            if (bootstrap == null) return;
            Camera cam = bootstrap.GetComponent<Camera>();
            if (cam == null) return;
            // 优先用 CameraRig.offset；否则回退 Bootstrap 默认 (0,14,14)，与 DeriveAxis 一致
            Vector3 offset = new Vector3(0f, 14f, 14f);
            CameraRig rig = cam.GetComponent<CameraRig>();
            if (rig != null && rig.offset.sqrMagnitude > 1e-6f) offset = rig.offset;
            DrawAxis(cam.transform.position, DepthSortBootstrap.DeriveAxis(offset));
        }

        static void DrawAxis(Vector3 origin, Vector3 axis)
        {
            Vector3 end = origin + axis * AxisLength;

            Color prevGizmo = Gizmos.color;
            Gizmos.color = AxisColor;
            Gizmos.DrawLine(origin, end);

            Color prevHandle = Handles.color;
            Handles.color = AxisColor;
            Handles.ArrowHandleCap(0, end, Quaternion.LookRotation(axis), 1f, EventType.Repaint);

            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.white;
            labelStyle.fontSize = 12;
            labelStyle.alignment = TextAnchor.MiddleLeft;
            Handles.Label(end + Vector3.up * 0.4f,
                string.Format("Y-Z Sort Axis\n({0:F3}, {1:F3}, {2:F3})", axis.x, axis.y, axis.z),
                labelStyle);

            Gizmos.color = prevGizmo;
            Handles.color = prevHandle;
        }
    }
}
#endif
