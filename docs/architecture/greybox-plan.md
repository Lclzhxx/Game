# Grey-Box 原型计划

> 目标：先验手感与帧率，再铺美术。用灰盒（capsule / plane）验证，不投手绘资源。
> 文档状态：Phase 2 工程落盘 · 已批准

---

## 1. 总目标

用 2 周冲刺，以灰盒占位（胶囊体 / 平面）验证**最高风险假设**，在投入任何手绘国风资源之前确认技术方向成立。原则：**先证明能玩、能稳帧，再花钱做美术。**

---

## 2. 优先验证的两条最高风险假设

1. **斜 45° 下 Z 分层战斗可读性**：玩家能否一眼分辨地面 / 空中威胁与攻击范围。
2. **墨韵栈在目标机型帧率**：完整墨水栈 + Height Fog 稳定 1080p60。

> 任一带不过 → 触发 Go/No-Go 的 No-Go（见末尾）。

---

## 3. 五条验证假设（含 Pass / Fail 判据）

| # | 假设 | Pass 判据 | Fail 判据 |
|---|------|-----------|-----------|
| H1 | Z 分层战斗可读性（斜 45°） | 测试者 5 秒内正确识别 ≥90% 地面/空中威胁与攻击范围 | 误判率 > 20% 或需主动旋转视角 |
| H2 | 墨韵栈帧率（1080p60） | 完整墨韵栈 + Height Fog 平均 ≥58fps（上限 60） | 平均 < 50fps 或帧时间波动 > 8ms |
| H3 | 国风着色器 + 屏幕墨线观感成立 | 内部评审 3/5 认可"国风成立"，墨线无脏化/双描边 | 普遍认为"像卡通描边"或描边脏化 |
| H4 | 打击感组合反馈足够 | 闪避/格挡/命中后主观"有冲击"，顿帧不卡死逻辑 | 反馈被评"软"或顿帧导致失帧 > 1 帧逻辑丢失 |
| H5 | 互锁箱庭相机 framing 可读性 | 2-3 连通区切换时关键信息不出框、不遮挡 | 关键目标频繁出镜或遮挡 |

---

## 4. 冲刺 Backlog（Story 列表 + 故事点）

| Story | 内容 | 故事点 |
|-------|------|--------|
| S1 | 相机 rig：低 FOV 斜 45° 锁定，禁旋转 | 2 |
| S2 | 灰盒箱庭：2-3 连通区 + 巡逻占位 | 3 |
| S3 | 角色灰盒 + 国风着色器最小版 | 3 |
| S4 | Z 分层碰撞 / 排序 | 3 |
| S5 | 闪避 / 格挡 / 弹幕最小实现（对象池） | 5 |
| S6 | Ink Render Feature 全屏 Pass（墨线/纸纹/渍墨/飞白） | 5 |
| S7 | Height Fog | 2 |
| S8 | 打击感组合（顿帧+墨溅+震屏+DOF） | 3 |
| S9 | 帧率埋点（1080p60 目标） | 2 |

合计：28 点（2 周 1 名程序 + 1 名技术美术可覆盖）。

---

## 5. Unity URP 项目搭建步骤

1. 新建 URP 项目，创建 `URPAsset` 与 `URPRenderer`（Forward+）。
2. 在 `URPRenderer` 上挂 `InkRenderFeature`（Renderer Feature）。
3. **锁定 Unity LTS 版本**（写进 `ProjectVersion.txt`，全队统一）。
4. 开启 **SRP Batcher**（Project Settings → Graphics）。
5. 建 Volume Profile：Color Grading（低饱和冷调）+ Height Fog。

---

## 6. 最小 C# 起步代码（真实可贴入 Unity）

### ① 相机 rig（锁定 Transform，低 FOV 透视斜 45°、禁旋转）

```csharp
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class FixedAngleCameraRig : MonoBehaviour
{
    [SerializeField] private float fov = 30f;          // 低 FOV
    [SerializeField] private Vector3 pivot = Vector3.zero;
    [SerializeField] private float distance = 18f;
    [SerializeField] private float height = 14f;       // 微俯

    private Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.allowHDR = false;
        cam.fieldOfView = fov;                          // 透视，禁正交
        // 斜 45° 微俯：固定方位，禁任何旋转输入
        Vector3 offset = new Vector3(0f, height, distance); // 斜俯
        transform.position = pivot + offset;
        transform.rotation = Quaternion.LookRotation(pivot - transform.position);
    }

    void LateUpdate() => transform.LookAt(pivot); // 仅跟踪目标，无自由旋转
}
```

### ② InkRenderFeature（ScriptableRendererFeature + RenderPass 骨架）

```csharp
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class InkRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent evt = RenderPassEvent.AfterRenderingPostProcessing;
        public float lineWidth = 1.0f;
        public float inkStrength = 0.8f;
        public float paperMultiply = 0.92f;
    }
    public Settings settings = new();

    class InkPass : ScriptableRenderPass
    {
        private Settings s;
        private RTHandle source;
        private RTHandle tmp;
        private Material inkMat; // Shader: 含 Sobel 墨线 + 纸纹 + 渍墨 + 飞白

        public InkPass(Settings s) { this.s = s; }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
        {
            source = data.cameraData.renderer.cameraColorTargetHandle;
            tmp = RTHandles.Alloc(Vector2.one, name: "_InkTmp"); // 半分辨率可选
        }

        public override void Execute(ScriptableRenderContext ctx, ref RenderingData data)
        {
            var cmd = CommandBufferPool.Get("InkPass");
            // 关键：Sobel 墨线基于深度+法线缓冲
            // float edge = Sobel(_CameraDepthNormals, uv, s.lineWidth);
            // float bleed = edge * Noise(uv * scale) * s.inkStrength;     // 墨渍化外扩
            // float paper = tex2D(_PaperTex, uv * tile) * s.paperMultiply; // 纸纹 Multiply
            // float feibai = step(Noise(uv), _Threshold);                 // 飞白打孔
            // color = lerp(color, inkColor, bleed) * paper * (1 - feibai);
            Blit(cmd, source, tmp, inkMat);   // 单全屏 Pass
            Blit(cmd, tmp, source);
            ctx.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose() => tmp?.Release();
    }

    private InkPass pass;
    public override void Create() => pass = new InkPass(settings);
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        => renderer.EnqueuePass(pass);
}
```

> ⚠️ 注意：Unity 6.x RenderGraph API 已变更，若升级需改写 `AddRenderPasses` 为 `RecordRenderGraph`。当前锁 LTS 版本，按上方经典 API 实现 + 写回归。

---

## 7. Go / No-Go

- **两条最高风险假设（H1、H2）任一 Fail** → **No-Go**：回 Phase 2 重审方向或换技术路线，**不进全量生产**。
- H3/H4/H5 任一 Fail → 可在生产期迭代修正，不阻断进入。
- 全部 Pass → 进入全量美术资源生产与系统实现。
