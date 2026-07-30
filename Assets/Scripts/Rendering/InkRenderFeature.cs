// =============================================================
// 文件：InkRenderFeature.cs
// 作用：经典 URP 后处理接入点（墨韵全屏效果）。
//       采用 Unity 2022.3 经典 API：ScriptableRendererFeature + ScriptableRenderPass，
//       【严禁】使用 Unity 6.x 的 RenderGraph / RecordRenderGraph（会导致整套编译失败）。
//
// 单路径兼容说明（核心原则：不依赖任何编辑器版本宏）：
//   本文件刻意【不出现】任何 #if UNITY_6000_0_OR_NEWER 之类的版本宏分支，
//   也【不引入】 RTHandle / RenderTargetHandle / RenderingUtils.ReAllocateIfNeeded /
//   Dispose 等版本敏感类型。只使用 2022.3（URP v14）与 Unity 6（URP v17）都稳定存在的
//   CommandBuffer 原生 API：
//     - ScriptableRenderer.cameraColorTarget            （始终使用；v17 下仅一条无害 obsolete 警告，可编译）
//     - CommandBuffer.GetTemporaryRT / Blit / SetGlobalTexture / ReleaseTemporaryRT
//     - CommandBufferPool.Get / Release
//   这样即便真实环境是「2022.3 编辑器 + 被升到 v17 的 URP 包」，宏开关也不会再判断错目标。
//
// 挂到：不是挂物体，而是"URP Renderer 资产"里的 Render Feature。
//       步骤：Project 里找到你的 URP-Renderer 资产 -> Add Renderer Feature -> 选 InkRenderFeature。
// Inspector 设置（在 Renderer 资产的 Feature 条目里）：
//   - inkMaterial：通过菜单【Greybox/Create Ink Material】生成材质后，拖到此字段。
//   - lineThickness / lineStrength / paperStrength / feibaiThreshold / inkStainStrength：调参用。
//   - enabled：是否启用墨韵效果。
// 着色器约定：InkFullscreen.shader 采样 _SourceTex（由本 Pass 经 SetGlobalTexture 喂入的"源屏幕色"），
//            深度来自 _CameraDepthTexture（由 ConfigureInput(Depth) 提供）。
// =============================================================

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#pragma warning disable CS0618 // 压制 ScriptableRenderer.cameraColorTarget 在 URP v17 下的 obsolete (CS0618) 警告；2022.3 下本就不触发，纯 no-op，跨版本安全。

public class InkRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class InkSettings
    {
        [Tooltip("墨韵材质：通过菜单 Greybox/Create Ink Material 生成材质后，拖到此字段；" +
                 "再在 URP Renderer 资产里 Add Renderer Feature 选 InkRenderFeature。")]
        public Material inkMaterial = null;            // 由 Inspector 指派（见上方注释）

        [Range(0.5f, 5f)] public float lineThickness = 1.0f;   // 墨线粗细（Sobel 采样半径）
        [Range(0f, 5f)]  public float lineStrength  = 1.0f;    // 墨线强度
        [Range(0f, 1f)]  public float paperStrength = 0.35f;    // 纸纹强度
        [Range(0f, 1f)]  public float feibaiThreshold = 0.7f;  // 飞白阈值（越高留白越少）
        [Range(0f, 2f)]  public float inkStainStrength = 0.6f; // 墨渍强度

        public bool enabled = true;
    }

    public InkSettings settings = new InkSettings();
    private InkRenderPass m_InkPass;

    // 经典 API：Create() 内创建 pass 实例。
    public override void Create()
    {
        m_InkPass = new InkRenderPass(settings);
        m_InkPass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents; // 场景画完后再上墨韵
    }

    // 经典 API：把 pass 入队。renderer.EnqueuePass 是 2022.3 / Unity 6 通用写法。
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!settings.enabled) return;

        if (settings.inkMaterial == null)
        {
            Debug.LogWarning("[InkRenderFeature] 未指派墨韵材质：请先点菜单 Greybox/Create Ink Material，" +
                            "再把生成的 Materials/InkMaterial.mat 拖到本 Feature 的 inkMaterial 字段。");
            return;
        }

        // 单代码路径：始终使用 renderer.cameraColorTarget（返回 RenderTargetIdentifier）。
        // 在 Unity 6（URP v17）下 cameraColorTarget 仅被标记为 [Obsolete]，仍返回 RenderTargetIdentifier，可编译；
        // 在 2022.3（URP v14）下为原生写法。两者共用同一行，无需版本宏分支。
        // （注意：若项目开启"Warning as Error"，v17 下该 obsolete 警告会升级为错误；见回传说明的处理建议。）
        m_InkPass.Setup(renderer.cameraColorTarget);
        renderer.EnqueuePass(m_InkPass);
    }

    // ---- 内部经典 RenderPass（单路径，无版本宏，无 RTHandle） ----
    class InkRenderPass : ScriptableRenderPass
    {
        private InkSettings m_Settings;
        private RenderTargetIdentifier m_CameraColorTarget; // 普通 RTI，无需 RTHandle / RenderTargetHandle

        public InkRenderPass(InkSettings settings)
        {
            m_Settings = settings;
        }

        public void Setup(RenderTargetIdentifier cameraColorTarget)
        {
            m_CameraColorTarget = cameraColorTarget;
        }

        // Configure：仅保证深度纹理可用（墨线靠深度 Sobel）。临时 RT 的 descriptor 在 Execute 里取。
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            ConfigureInput(ScriptableRenderPassInput.Depth); // => _CameraDepthTexture 被填充
        }

        // Execute：先把当前屏幕色拷到临时 RT，再把"源色"喂给全局 _SourceTex，
        //          最后用墨韵材质 + 深度处理后写回屏幕。全部使用 2022.3 / Unity 6 都稳定的 CommandBuffer API。
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Material mat = m_Settings.inkMaterial;
            if (mat == null) return;

            // 同步可调参数（每帧从 Inspector 读，改参数即时生效）
            mat.SetFloat("_LineThickness", m_Settings.lineThickness);
            mat.SetFloat("_LineStrength",  m_Settings.lineStrength);
            mat.SetFloat("_PaperStrength", m_Settings.paperStrength);
            mat.SetFloat("_FeibaiThreshold", m_Settings.feibaiThreshold);
            mat.SetFloat("_InkStainStrength", m_Settings.inkStainStrength);

            CommandBuffer cmd = CommandBufferPool.Get("InkRenderPass");
            int tempID = Shader.PropertyToID("_InkTempRT");
            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0; // 颜色临时 RT 不需要深度
            cmd.GetTemporaryRT(tempID, desc);
            cmd.Blit(m_CameraColorTarget, tempID);                                                       // 屏幕色 -> 临时 RT
            cmd.SetGlobalTexture(Shader.PropertyToID("_SourceTex"), new RenderTargetIdentifier(tempID)); // 喂给着色器 _SourceTex
            cmd.Blit(tempID, m_CameraColorTarget, mat);                                                 // 临时RT(源色)+深度 -> 经墨韵材质 -> 写回屏幕
            cmd.ReleaseTemporaryRT(tempID);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
