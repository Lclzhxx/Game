// =============================================================
// 文件：ToonReviewBuilder.cs（E1-S2 子任务 4，ADR-008 验收场景构建器）
// 作用：一键代码化搭建「国风 Toon 验收场景」——
//       球 / 胶囊 / 带笔触法线的平面 × 主平行光 + 1 盏点光 + 固定 45° 相机。
//       用途：① Toon 明暗分档/水墨阴影色/Rim 的肉眼判读；
//             ② 与墨韵 Pass 共存验收（勾线只来自墨韵，材质零描边 —— R5）；
//             ③ 截图基线 toon_baseline.png 的取景源（构图必须稳定，故相机关平滑）。
// 使用：
//   方式一：打开 Assets/Tests/Scenes/ToonReview.unity 直接点 ▶（场景里已挂本脚本）。
//   方式二：任意场景挂本脚本运行。
//   方式三：编辑器菜单 MJ/Test/Build Toon Review Scene（不进 Play 也能搭，便于截图）。
// 判读标准（肉眼）：
//   - 球/胶囊出现【清晰的 2 档明暗分界】，暗部呈冷灰偏青的"淡墨"而非死黑；
//   - 边缘有 Rim 亮边勾勒剪影；
//   - 平面（开笔触法线）的明暗交界呈毛笔干湿的不规则抖动；
//   - 点光在暗侧打出一档柔和亮块；
//   - 物体轮廓线【只有一层】——若出现双线即 R5 违规（材质混入了描边）。
// 材质：走 MJ.Rendering.ToonMaterialDefaults（与 ToonGuofeng_Default.mat 同一套参数）。
// 注意：运行时脚本，不引用 UnityEditor；需在 Unity 2022.3 下编译。
// =============================================================

using MJ.Rendering;
using UnityEngine;

[ExecuteInEditMode] // 编辑态打开场景即自动搭建，球/胶囊无需进 Play 即可在 Scene 视图看到（便于 H3 评审与排查）
public class ToonReviewBuilder : MonoBehaviour
{
    public const string ROOT_NAME = "ToonReview";

    [Header("自动构建（场景加载即搭）")]
    public bool autoBuildOnStart = true;

    [Header("相机：固定 45 度俯视，构图必须稳定（截图基线依赖）")]
    public Vector3 cameraOffset = new Vector3(0f, 6f, 6f);

    [Header("点光（暗侧补一档柔光，验证附加光分档）")]
    public Color pointLightColor = new Color(0.75f, 0.82f, 0.95f, 1f);
    public float pointLightIntensity = 3f;
    public float pointLightRange = 12f;

    void Start()
    {
        // 无条件构建：Build() 内部会先销毁已存在的 ToonReview 组，
        // 避免「场景残留旧组」导致跳过重建、相机仍指向已销毁 focus 的取景失效。
        if (autoBuildOnStart)
            Build();
    }

    public void Build()
    {
        GameObject old = GameObject.Find(ROOT_NAME);
        if (old != null)
        {
            if (Application.isPlaying) Destroy(old);
            else DestroyImmediate(old);
        }

        GameObject root = new GameObject(ROOT_NAME);

        // ---- 两份材质：普通（球/胶囊）与开笔触法线（平面） ----
        Material toonMat = ToonMaterialDefaults.CreateMaterial();
        Material toonBrushMat = ToonMaterialDefaults.CreateMaterial(false, true);
        if (toonMat == null || toonBrushMat == null)
        {
            Debug.LogError("[ToonReviewBuilder] Toon 材质创建失败（shader 未编译？），场景搭建中止。");
            return;
        }
        if (toonBrushMat != null)
        {
            Texture2D brush = ToonMaterialDefaults.CreateProceduralBrushNormal();
            toonBrushMat.SetTexture("_BrushNormalMap", brush);
            toonBrushMat.SetTextureScale("_BrushNormalMap", new Vector2(6f, 6f)); // 铺开笔锋密度
        }

        // ---- 带笔触法线的地面平面（Toon 材质，非默认 Lit） ----
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "BrushPlane";
        plane.transform.SetParent(root.transform, false);
        plane.transform.localScale = new Vector3(2f, 1f, 2f);
        StripCollider(plane);
        plane.GetComponent<Renderer>().sharedMaterial = toonBrushMat;

        // ---- 球：判读 ramp 分档最直观的体 ----
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "ToonSphere";
        sphere.transform.SetParent(root.transform, false);
        sphere.transform.localPosition = new Vector3(-1.6f, 1f, 0f);
        sphere.transform.localScale = Vector3.one * 2f;
        StripCollider(sphere);
        sphere.GetComponent<Renderer>().sharedMaterial = toonMat;

        // ---- 胶囊：判读 Rim 与拉长曲面上的交界走向 ----
        GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.name = "ToonCapsule";
        capsule.transform.SetParent(root.transform, false);
        capsule.transform.localPosition = new Vector3(1.6f, 1.2f, 0f);
        capsule.transform.localScale = Vector3.one * 1.3f;
        StripCollider(capsule);
        capsule.GetComponent<Renderer>().sharedMaterial = toonMat;

        // ---- 主平行光（含阴影：验证 ShadowCaster pass） ----
        GameObject sunGO = new GameObject("MainLight");
        sunGO.transform.SetParent(root.transform, false);
        Light sun = sunGO.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.1f;
        sun.color = new Color(1f, 0.97f, 0.92f, 1f);
        sun.shadows = LightShadows.Soft;
        sunGO.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

        // ---- 1 盏点光（性能契约 §5：950M 上限 1–3 盏；这里只放 1 盏） ----
        GameObject pointGO = new GameObject("FillPointLight");
        pointGO.transform.SetParent(root.transform, false);
        pointGO.transform.localPosition = new Vector3(2.5f, 2.2f, 3f);
        Light point = pointGO.AddComponent<Light>();
        point.type = LightType.Point;
        point.color = pointLightColor;
        point.intensity = pointLightIntensity;
        point.range = pointLightRange;
        point.shadows = LightShadows.None; // 附加光阴影在 950M 上不开（性能契约）

        // ---- 相机：与生产同款 CameraRig，关平滑保证截图构图逐帧一致 ----
        GameObject focus = new GameObject("CameraFocus");
        focus.transform.SetParent(root.transform, false);
        focus.transform.localPosition = new Vector3(0f, 1f, 0f);

        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            cam = camGO.AddComponent<Camera>();
        }
        CameraRig rig = cam.GetComponent<CameraRig>();
        if (rig == null) rig = cam.gameObject.AddComponent<CameraRig>();
        rig.target = focus.transform;
        rig.offset = cameraOffset;
        rig.useSmoothing = false; // 基线截图：构图必须稳定

        // 同步摆位：不依赖 CameraRig 的 Awake/LateUpdate 时机（运行时 AddComponent 的
        // Awake 延后到下一帧，首帧渲染时相机未摆正 => Game 视图蓝屏、看不到球/胶囊）。
        // 这里立即写死机位与朝向，确保首帧即正确取景。
        cam.transform.position = focus.transform.position + cameraOffset;
        cam.transform.LookAt(focus.transform.position);
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1000f;
        cam.enabled = true;
        cam.gameObject.SetActive(true);

        // 相机也参与 Y-Z 排序接线（与生产一致；Toon 为不透明，此处只为环境一致性）
        if (cam.GetComponent<DepthSortBootstrap>() == null)
            cam.gameObject.AddComponent<DepthSortBootstrap>();

        Debug.Log("[ToonReviewBuilder] Toon 验收场景已搭建。判读：球/胶囊 2 档分明、暗部冷灰不死黑、" +
                  "Rim 勾剪影、平面交界有笔触抖动；轮廓线只有一层（双线=R5 违规）。");
    }

    private static void StripCollider(GameObject go)
    {
        Collider col = go.GetComponent<Collider>();
        if (col == null) return;
        if (Application.isPlaying) Destroy(col);
        else DestroyImmediate(col);
    }
}
