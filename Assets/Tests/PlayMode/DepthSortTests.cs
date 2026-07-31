// =============================================================
// 文件：DepthSortTests.cs（PlayMode 测试，E2-S2 / ADR-009）
// 作用：Y-Z 深度排序验收——纯相机状态断言，不依赖渲染输出，CI 可跑。
// 覆盖：
//   1. DepthSortBootstrap 把相机置为 CustomAxis，且轴 = -offset.normalized（单一事实来源推导）。
//   2. offset 改动 → 轴自动跟随（不写死轴值）。
//   3. GreyboxBuilder.BuildScene() 自动接线（主相机必带 DepthSortBootstrap）。
//   4. 零每帧成本：DepthSortBootstrap 不含 Update/LateUpdate（反射断言，防回归）。
// 轴符号说明（对齐 Unity CustomAxis 语义：投影值大者视为更远、先绘制）：
//   相机在 +Y+Z 高处看向 -Y-Z ⇒ 更远的物体 y+z 更小 ⇒ 轴必须取 (0,-1,-1) 方向，
//   即 -offset.normalized ≈ (0, -0.7071, -0.7071)。ADR-009 正文 "-offset.normalized"
//   为准；其代码示例中 (0,1,1) 为符号笔误（已修正）。最终以 SortingReview 场景真机肉眼终验（C4）。
// =============================================================

using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class DepthSortTests
{
    private const float Eps = 1e-4f;

    private static GameObject MakeCameraGO(Vector3 offset)
    {
        var go = new GameObject("TestCamera_DepthSort");
        go.AddComponent<Camera>();
        var rig = go.AddComponent<CameraRig>();
        rig.offset = offset;
        go.AddComponent<DepthSortBootstrap>(); // AddComponent 立即触发 Awake → 一次性配置
        return go;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>())
        {
            if (go.name == "TestCamera_DepthSort" || go.name == "Greybox" || go.name == "Main Camera")
                Object.Destroy(go);
        }
    }

    // ---------- 验收 1：CustomAxis + 轴值（默认 offset (0,14,14) → 45°） ----------

    [UnityTest]
    public IEnumerator Bootstrap_SetsCustomAxis_FromDefaultOffset()
    {
        var go = MakeCameraGO(new Vector3(0f, 14f, 14f));
        yield return null;

        var cam = go.GetComponent<Camera>();
        Assert.AreEqual(TransparencySortMode.CustomAxis, cam.transparencySortMode);

        Vector3 expected = -(new Vector3(0f, 14f, 14f)).normalized; // (0, -0.7071, -0.7071)
        Assert.AreEqual(expected.x, cam.transparencySortAxis.x, Eps);
        Assert.AreEqual(expected.y, cam.transparencySortAxis.y, Eps);
        Assert.AreEqual(expected.z, cam.transparencySortAxis.z, Eps);
        Assert.AreEqual(0.7071f, Mathf.Abs(cam.transparencySortAxis.y), 1e-3f, "45° 下 |y| 必须 ≈ 0.7071");
        Assert.AreEqual(0.7071f, Mathf.Abs(cam.transparencySortAxis.z), 1e-3f, "45° 下 |z| 必须 ≈ 0.7071");
        Assert.AreEqual(1f, cam.transparencySortAxis.magnitude, Eps, "轴必须归一化");
    }

    // ---------- 验收 2：轴随 offset 推导（单一事实来源，不写死） ----------

    [UnityTest]
    public IEnumerator Bootstrap_AxisFollowsRigOffset()
    {
        var go = MakeCameraGO(new Vector3(0f, 10f, 5f));
        yield return null;

        var cam = go.GetComponent<Camera>();
        Vector3 expected = -(new Vector3(0f, 10f, 5f)).normalized;
        Assert.AreEqual(TransparencySortMode.CustomAxis, cam.transparencySortMode);
        Assert.AreEqual(expected.x, cam.transparencySortAxis.x, Eps);
        Assert.AreEqual(expected.y, cam.transparencySortAxis.y, Eps);
        Assert.AreEqual(expected.z, cam.transparencySortAxis.z, Eps);
    }

    // ---------- 验收 3：GreyboxBuilder 自动接线 ----------

    [UnityTest]
    public IEnumerator GreyboxBuilder_WiresBootstrapOnMainCamera()
    {
        GreyboxBuilder.BuildScene();
        yield return null;

        Camera cam = Camera.main;
        Assert.IsNotNull(cam, "GreyboxBuilder 必须产出主相机");
        Assert.IsNotNull(cam.GetComponent<DepthSortBootstrap>(), "主相机必须自动挂 DepthSortBootstrap");
        Assert.AreEqual(TransparencySortMode.CustomAxis, cam.transparencySortMode);
    }

    // ---------- 验收 4：零每帧成本（无 Update 族方法，防回归） ----------

    [Test]
    public void Bootstrap_HasNoPerFrameCallbacks()
    {
        var t = typeof(DepthSortBootstrap);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        Assert.IsNull(t.GetMethod("Update", flags), "零每帧成本红线：禁止 Update");
        Assert.IsNull(t.GetMethod("LateUpdate", flags), "零每帧成本红线：禁止 LateUpdate");
        Assert.IsNull(t.GetMethod("FixedUpdate", flags), "零每帧成本红线：禁止 FixedUpdate");
    }
}
