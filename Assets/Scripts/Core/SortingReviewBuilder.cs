// =============================================================
// 文件：SortingReviewBuilder.cs（E2-S2，ADR-009 验收场景构建器）
// 作用：一键代码化搭建「排序验收场景」——3 组前后站位透明面片
//       + 1 个 SortingGroup 多面片组合体 + 主相机（CameraRig + DepthSortBootstrap）。
//       用于 C4 验收：斜 45° 下透明队列无穿插错排（肉眼 + 截图基线）。
// 使用：
//   方式一：打开 Assets/Tests/Scenes/SortingReview.unity 直接点 ▶（场景里已挂本脚本）。
//   方式二：任意场景挂本脚本运行。
// 判读标准（肉眼）：每组面片中【后位（z 小、颜色深）】必须被【前位（z 大、颜色浅）】遮挡；
//   组合体（三片十字排列）整体前后移动时构件间不得互穿。
// 材质说明：测试面片用 Sprites/Default（透明队列、双面、URP 兼容），
//   仅限验收场景使用——正式资产必须走 URP 材质规范（控制清单）。
// 注意：运行时脚本，不引用 UnityEditor；需在 Unity 2022.3 下编译。
// =============================================================

using UnityEngine;
using UnityEngine.Rendering;

public class SortingReviewBuilder : MonoBehaviour
{
    private const string ROOT_NAME = "SortingReview";

    [Header("自动构建（场景加载即搭）")]
    public bool autoBuildOnStart = true;

    [Header("组合体来回移动（观察构件是否互穿），0=静止")]
    public float compositeMoveAmplitude = 3f;
    public float compositeMoveSpeed = 0.5f;

    private Transform m_Composite;
    private Vector3 m_CompositeHome;

    void Start()
    {
        if (autoBuildOnStart && GameObject.Find(ROOT_NAME) == null)
            Build();
    }

    void Update()
    {
        // 仅验收场景的演示位移（组合体整体前后移动）；非生产代码路径。
        if (m_Composite != null && compositeMoveAmplitude > 0f)
        {
            float t = Mathf.Sin(Time.time * compositeMoveSpeed * Mathf.PI * 2f);
            m_Composite.position = m_CompositeHome + new Vector3(0f, 0f, t * compositeMoveAmplitude);
        }
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

        // 地面 + 主光（最小环境，便于判读遮挡关系）
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.parent = root.transform;
        ground.transform.localScale = new Vector3(4f, 1f, 4f);

        GameObject lightGO = new GameObject("DirectionalLight");
        lightGO.transform.parent = root.transform;
        Light light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ---- 3 组前后站位透明面片：后位深红（必须被遮）、前位浅青 ----
        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * 4f;
            MakeQuad(root, "Pair" + i + "_Back", new Vector3(x, 1f, -1.5f),
                new Color(0.7f, 0.15f, 0.1f, 0.8f), 2f);
            MakeQuad(root, "Pair" + i + "_Front", new Vector3(x, 1f, 1.5f),
                new Color(0.4f, 0.9f, 0.85f, 0.8f), 2f);
        }

        // ---- SortingGroup 组合体：三片十字排列，组内 sortingOrder 静态分层 ----
        GameObject composite = new GameObject("Composite_SortingGroup");
        composite.transform.parent = root.transform;
        composite.transform.position = new Vector3(0f, 1f, 5f);
        composite.AddComponent<SortingGroup>(); // 控制清单：多面片组合体必挂 SortingGroup
        MakeQuad(composite, "Body", Vector3.zero, new Color(0.9f, 0.8f, 0.3f, 0.85f), 2.4f, 0);
        MakeQuad(composite, "Sash", new Vector3(0.5f, -0.2f, 0.01f), new Color(0.2f, 0.3f, 0.8f, 0.85f), 1.6f, 1);
        MakeQuad(composite, "Blade", new Vector3(-0.7f, 0.4f, -0.01f), new Color(0.85f, 0.85f, 0.9f, 0.85f), 1.2f, 2);
        m_Composite = composite.transform;
        m_CompositeHome = composite.transform.position;

        // ---- 相机：焦点空物体 + CameraRig + DepthSortBootstrap（与生产接线一致） ----
        GameObject focus = new GameObject("CameraFocus");
        focus.transform.parent = root.transform;
        focus.transform.position = new Vector3(0f, 1f, 1f);

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
        rig.offset = new Vector3(0f, 14f, 14f);
        rig.useSmoothing = false; // 验收场景：稳定构图便于截图基线

        if (cam.GetComponent<DepthSortBootstrap>() == null)
            cam.gameObject.AddComponent<DepthSortBootstrap>();
        else
            DepthSortBootstrap.Apply(cam); // 已存在则强制按当前 offset 重推一次

        Debug.Log("[SortingReviewBuilder] 排序验收场景已搭建。判读：各组深红后片必须被浅青前片遮挡；" +
                  "组合体前后移动构件不得互穿（C4）。");
    }

    // 透明面片：Quad + Sprites/Default（透明队列 3000、双面、URP 兼容）
    private static void MakeQuad(GameObject parent, string name, Vector3 localPos, Color color, float size, int sortingOrder = 0)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.transform.parent = parent.transform;
        quad.transform.localPosition = localPos;
        quad.transform.localScale = new Vector3(size, size, 1f);
        // 面片立在 XZ 世界中朝向 +Z（相机在 +Z 侧俯视），Sprites/Default 双面渲染，朝向不敏感。
        Collider col = quad.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Object.Destroy(col);
            else Object.DestroyImmediate(col); // 编辑器菜单直调 Build() 时的安全路径
        }

        Renderer r = quad.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = color;
        r.material = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        if (sortingOrder != 0) r.sortingOrder = sortingOrder; // 组内静态分层（ADR-009）
    }
}
