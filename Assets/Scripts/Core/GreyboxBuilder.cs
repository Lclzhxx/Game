// =============================================================
// 文件：GreyboxBuilder.cs
// 作用：一键代码化搭建灰盒场景，使【非程序员无需手动摆放任何物体】。
//       构建内容：大地面 + 互锁连通箱庭（房间A → 窄门洞 → 房间B → 上坡 → 房间C 高台）
//       + 房间A 放地面敌人、房间B 放空中敌人（浮空）、房间C 放 Boss 占位（红大方块）
//       + 玩家（含 PlayerController）+ 主方向光 + 环境光 + 自动挂好 CameraRig / FpsProbe。
// 使用：
//   方式一（推荐）：Unity 菜单 -> Greybox/Rebuild Scene（菜单入口在 Scripts/Editor/GreyboxMenu.cs）。
//   方式二（可选）：把本脚本拖到一个空物体上 -> 运行时若场景未构建则自动构建。
// 挂到：空物体（仅用于"方式二"自动构建）；"方式一"无需挂物体。
// 注意：本脚本是【运行时】脚本，绝不能引用 UnityEditor（否则非 Editor 文件夹下编译失败）。
// 注意：需在 Unity 2022.3 下编译（经典 GameObject / PrimitiveType API，不生成 .unity 文件）。
// =============================================================

using UnityEngine;
using System;

public class GreyboxBuilder : MonoBehaviour
{
    private const string ROOT_NAME = "Greybox";

    [Header("自动构建（仅方式二：挂到物体后运行时生效）")]
    public bool autoBuildOnStart = true;

    // 菜单入口在 Scripts/Editor/GreyboxMenu.cs（避免运行时脚本引用 UnityEditor 导致编译失败）。

    void Start()
    {
        if (autoBuildOnStart && GameObject.Find(ROOT_NAME) == null)
            BuildScene();
    }

    public static void BuildScene()
    {
        // 先清旧的，避免重复
        GameObject old = GameObject.Find(ROOT_NAME);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);

        GameObject root = new GameObject(ROOT_NAME);
        root.tag = "EditorOnly"; // 内置标签，绝不报错

        // 地面（接收阴影）
        GameObject ground = CreatePrimitive(PrimitiveType.Plane, root, "Ground", new Vector3(0, 0, 0));
        ground.transform.localScale = new Vector3(12f, 1f, 12f); // 60 x 60
        SetColor(ground, new Color(0.8f, 0.8f, 0.8f));

        // 灯光
        GameObject lightGO = new GameObject("DirectionalLight");
        lightGO.transform.parent = root.transform;
        Light light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.4f;
        light.shadows = LightShadows.Soft;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.5f); // 纯保险，方向光才是主光源

        // 互锁连通箱庭
        BuildLayout(root);

        // 敌人：房间A 地面敌人（绿）/ 房间B 空中敌人（青，浮空）/ 房间C Boss 占位（红大方块）
        CreateEnemy(root, "Dummy_Ground", new Vector3(-12f, 1f, 0f), false, 0f,  Color.green);
        CreateEnemy(root, "Dummy_Air", new Vector3(0f, 2.5f, 0f), true, 2.5f, Color.cyan);
        GameObject boss = CreatePrimitive(PrimitiveType.Cube, root, "BossPlaceholder", new Vector3(16f, 4f, 0f));
        boss.transform.localScale = new Vector3(4f, 4f, 4f);
        SetColor(boss, Color.red);

        // 玩家
        GameObject player = CreatePlayer(root, new Vector3(-12f, 1f, -6f));

        // 主相机：自动挂 CameraRig + FpsProbe 并设好 target（用户零配置）
        SetupCamera(player.transform);

        Debug.Log("[GreyboxBuilder] 场景已重建。点运行即可验证 H1（斜45°下 Z 分层可读性）与 H2（墨韵栈 1080p60 帧率）。");
    }

    // ---------------- 布局：房间 A -> 门洞 -> 房间 B -> 门洞 -> 上坡 -> 房间 C 高台 ----------------
    static void BuildLayout(GameObject root)
    {
        float h = 3f;
        // 后墙（相机在 +z 看向 -z，后墙在 -z，前侧留空便于观察）
        Wall(root, "Wall_Back",  0f, -10f, 60f, 1f, h);
        // 左右侧墙
        Wall(root, "Wall_Left", -30f, 0f, 1f, 21f, h);
        Wall(root, "Wall_Right", 30f, 0f, 1f, 21f, h);
        // 分隔墙 x=-8（A|B），留门洞 z ∈ [-1.5, 1.5]
        Wall(root, "Wall_Div1A", -8f, -5.75f, 1f, 8.5f, h);
        Wall(root, "Wall_Div1B", -8f,  5.75f, 1f, 8.5f, h);
        // 分隔墙 x=7（B|C），留门洞
        Wall(root, "Wall_Div2A",  7f, -5.75f, 1f, 8.5f, h);
        Wall(root, "Wall_Div2B",  7f,  5.75f, 1f, 8.5f, h);

        // 房间 C 高台 + 上坡（验证互锁箱庭相机构图）
        Wall(root, "Platform_C", 16f, 0f, 12f, 12f, 2f); // 顶面 y=2
        GameObject ramp = CreatePrimitive(PrimitiveType.Cube, root, "Ramp_C", new Vector3(9f, 1f, 0f));
        ramp.transform.localScale = new Vector3(6f, 0.4f, 6f);
        ramp.transform.rotation = Quaternion.Euler(0f, 0f, -18f); // 缓坡，CharacterController 可上
        SetColor(ramp, new Color(0.6f, 0.6f, 0.62f));
    }

    // ---------------- 主相机零配置 ----------------
    static void SetupCamera(Transform player)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            cam = camGO.AddComponent<Camera>();
        }
        CameraRig rig = cam.GetComponent<CameraRig>();
        if (rig == null) rig = cam.gameObject.AddComponent<CameraRig>();
        rig.target = player;
        rig.offset = new Vector3(0f, 14f, 14f);
        rig.useSmoothing = true;

        if (cam.GetComponent<FpsProbe>() == null)
            cam.gameObject.AddComponent<FpsProbe>();

        // E2-S2（ADR-009）：Y-Z 透明排序轴，一次性初始化零每帧成本。
        // 注意顺序：必须在 rig.offset 赋值之后挂载（Awake 里从 offset 推导轴）。
        if (cam.GetComponent<DepthSortBootstrap>() == null)
            cam.gameObject.AddComponent<DepthSortBootstrap>();
    }

    // ---------------- 工具函数 ----------------
    static GameObject CreatePrimitive(PrimitiveType type, GameObject root, string name, Vector3? pos = null)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.parent = root.transform;
        if (pos.HasValue) go.transform.position = pos.Value;
        return go;
    }

    static GameObject Wall(GameObject root, string name, float x, float z, float w, float d, float h)
    {
        GameObject go = CreatePrimitive(PrimitiveType.Cube, root, name, new Vector3(x, h / 2f, z));
        go.transform.localScale = new Vector3(w, h, d);
        SetColor(go, new Color(0.6f, 0.6f, 0.62f));
        return go;
    }

    static GameObject CreateEnemy(GameObject root, string name, Vector3 pos, bool flying, float hover, Color color)
    {
        GameObject e = CreatePrimitive(PrimitiveType.Capsule, root, name, pos);
        e.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
        DummyEnemy de = e.AddComponent<DummyEnemy>();
        de.isFlying = flying;
        de.hoverHeight = hover;
        de.chasePlayer = false; // 灰盒：待机，专注验证可读性
        SetColor(e, color);
        return e;
    }

    static GameObject CreatePlayer(GameObject root, Vector3 pos)
    {
        GameObject p = CreatePrimitive(PrimitiveType.Capsule, root, "Player");
        p.transform.position = pos;
        p.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
        p.tag = "Player"; // 内置标签，安全

        // 移除默认 CapsuleCollider，改用 CharacterController（比 Rigidbody 更不易卡 bug）
        Collider def = p.GetComponent<Collider>();
        if (def != null) UnityEngine.Object.DestroyImmediate(def);
        CharacterController cc = p.AddComponent<CharacterController>();
        cc.radius = 0.4f;
        cc.height = 1.8f;
        cc.center = Vector3.zero;

        p.AddComponent<PlayerController>();
        SetColor(p, Color.yellow);
        return p;
    }

    static void SetColor(GameObject go, Color c)
    {
        Renderer r = go.GetComponent<Renderer>();
        if (r != null) r.material.color = c;
    }
}
