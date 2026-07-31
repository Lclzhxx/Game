# ADR-010 高度雾与墨韵 Render Feature 的集成点（E1-S3）

> 状态：**已采纳（含已批准实现偏差）**
> 关联：ADR-002（墨韵单 Pass）、C2（严禁多 Pass 叠加）、C5（跨版本单路径）、性能契约§5（Height Fog < 1ms，禁真体积 raymarch；墨韵栈 < 2–3ms）
> 引擎钉定：Unity 2022.3.62 · URP 14.0.12（URP 14 无内建高度雾，必须自建）

## 上下文

- 需求：基于**世界空间高度**的雾（山谷云海、低洼墨气），呼应国风水墨「留白/晕染」。
- 现状：墨韵栈 = 单条全屏 Pass（`InkRenderFeature`，AfterRenderingTransparents），Execute 内已做 `屏幕色→临时RT→(墨韵材质)→写回` 两次 Blit，且已 `ConfigureInput(Depth)`，**深度纹理现成**。
- 硬约束：C2 明令禁止多条全屏 Pass 叠加；950M 上每多一对全屏 Blit ≈ 0.3–0.8ms 带宽开销，预算里根本没有第二条 Pass 的位置。

## 备选方案

1. **独立的 HeightFogRenderFeature（第二条全屏 Pass）**——模块干净，但直接违背 C2，950M 上新增一对 Blit 带宽；两条 Pass 的执行顺序/RT 交接又引入新的耦合面。❌
2. **逐材质雾（在 ToonGuofeng 等物体 shader 里算雾）**——零全屏成本；但天空/粒子/未来第三方 shader 全要各自接，覆盖不全、必然漏（半透明特效尤其难保一致），维护面发散。⚠️（保留 ADR-008 中的 no-op 钩子作 v2 备份路径）
3. **并入墨韵全屏 Pass：在 `InkFullscreen.shader` 内新增「高度雾阶段」，keyword 门控**——复用已有的深度采样与两次 Blit，**零新增 Pass、零新增 Blit**；雾在墨线/纸纹合成**之前**作用于源色，勾线叠在雾上，正确对应「先晕染后勾勒」的水墨作画顺序。✅
4. *真体积雾 raymarch*——预算红线明令禁止（§5）。❌

## 决定

采用**方案 3**：高度雾作为墨韵全屏 Pass 内的一个**前置合成阶段**。

### Shader 侧（`InkFullscreen.shader`）

- 新增 `#pragma shader_feature_local _MJ_HEIGHT_FOG`；关雾时零成本（变体剔除）。
- frag 流程变为：`源色 → [高度雾混合] → 墨线/纸纹/飞白/墨渍 → 输出`。
- 世界坐标重建：采样 `_CameraDepthTexture` → NDC → `mul(unity_MatrixInvVP, ...)` 取 `positionWS.y`（URP 14 经典做法，无版本宏，C5 安全）。
- 雾模型（解析式，无 raymarch）：指数高度雾
  `fogFactor = _FogDensity * exp(-_FogHeightFalloff * (posWS.y - _FogBaseHeight)) * saturate(viewDist / _FogDistFade)`，
  `color = lerp(color, _FogColor, saturate(fogFactor))`。
  参数：`_FogColor`（默认淡墨青灰）、`_FogBaseHeight`、`_FogDensity`、`_FogHeightFalloff`、`_FogDistFade`。
- 天空（depth≈far）按 `_FogSkyBlend` 单独控制混合上限，避免天空糊死。

### C# 侧（`InkRenderFeature.cs`，小幅扩展）

- `InkSettings` 新增 `HeightFogSettings` 子块（enabled + 上述 5–6 个参数，带 `Range` 与 clamp，遵循 E1-S1 越界保护先例）。
- Execute 内 `mat.SetFloat/SetColor` 同步 + `CoreUtils.SetKeyword(mat, "_MJ_HEIGHT_FOG", fog.enabled)`。
- **不新增** RenderPass / RT / Blit——改动面被刻意压到最小。
- Volume 色调（Color Grading 低饱和冷调）维持走 URP 内建 Volume（灰盒 S7 已配），与本雾无代码耦合，只做参数联调。

### 验证（CI 可跑部分）

- EditMode：材质开关 keyword 断言、参数 clamp 断言（复用 E1-S1 的 ArgumentGuard 模式）。
- 回归：墨韵截图基线**新增一张开雾基线**（`ink_fog_baseline.png`）；耗时断言维持「墨韵栈整体 < 3ms」——雾并入后共用同一预算口径（雾自身增量目标 < 0.5ms，950M 实测回填）。
- 真机：制作人 S2 试玩窗口肉眼验收「低洼有墨气、高台清透、勾线不糊」。

## 后果

- ✅ 严守 C2 单 Pass 红线；950M 零新增带宽；深度采样/参数同步/回归设施全部复用。
- ✅ 关 keyword 即完全回退，风险可控。
- ⚠️ `InkFullscreen.shader` 职责变厚（勾线+纸纹+雾）——用 HLSL 函数分段 + 注释分区管理；若未来再并入第 4 个效果，须重开 ADR 评估拆分。
- ⚠️ 雾作用于全屏源色，**发生在 Toon 光照之后**：逐材质的精细雾（如仅某物体免疫雾）不支持——接受，个别演出需求走 ADR-008 预留的 no-op 钩子（v2）。
- ⚠️ 墨韵回归基线图 +1 张，基线管理成本微增。

## 实施记录（E1-S3，S2·W2）

### 与本 ADR 原文的一处偏差 —— 已裁定：接受偏差（制作人批准）

| 项 | ADR 原文 | 实施 | 理由 |
|---|---|---|---|
| keyword 声明方式 | `#pragma shader_feature_local _MJ_HEIGHT_FOG` | `#pragma multi_compile_local_fragment _ _MJ_HEIGHT_FOG` | `shader_feature` 的变体入包依据是**材质资产落盘时的 keyword 状态**。本雾的 keyword 由 `InkRenderFeature` 在 **Execute 里运行时** `CoreUtils.SetKeyword` 开关，材质资产上永远是关的 → 构建期 `_MJ_HEIGHT_FOG` 变体会被剥掉，**编辑器里开雾正常、真机出包无效**（典型的"编辑器绿、真机黑"）。`multi_compile` 固定编译 2 个变体，关雾变体与 S1 完全同构，关雾态零成本不变；代价仅是变体数 ×2（本 shader 无其他 multi_compile，绝对量 = 2）。 |

裁定（2026-07-31，S2·W2，制作人批准）：采用 **选项 1（接受偏差）**。
- 理由：运行时 `CoreUtils.SetKeyword` 开关 keyword，`shader_feature_local` 因材质资产落盘时 keyword 恒为关，会被构建期剥离，诱发「编辑器绿、真机黑」；`multi_compile_local_fragment` 固定编译 2 变体，关雾态零成本，安全。
- 代价仅是变体数 ×2（本 shader 无其他 multi_compile，绝对量 = 2），可接受。
- ADR 正文 §决定/Shader 侧 第 26 行的 `shader_feature_local` 以本偏差表为权威修正——实施以 `multi_compile_local_fragment` 为准；本 ADR 视为「已采纳（含已批准实现偏差）」。

### 实施细节增补

- 参数落地为 6 个：`_FogColor` / `_FogBaseHeight` / `_FogDensity` / `_FogHeightFalloff` / `_FogDistFade` / `_FogSkyBlend`。
- 世界坐标重建统一走 Core RP `ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP)`（URP14 里 `UNITY_MATRIX_I_VP` 即 `unity_MatrixInvVP`），不手搓矩阵、不写版本宏（C5）。
- `HeightFogSettings` **独立成文件**（`Assets/Scripts/Rendering/HeightFogSettings.cs`）而非嵌套在 `InkRenderFeature` 内：
  嵌套类型会迫使 EditMode 测试程序集连带引用 URP 程序集（CS0012），独立后测试只依赖 `MJ.Runtime`。
- `Guard()` 刻意 `public static`：既是运行时防护，也是 EditMode 参数化断言的入口。
- Volume 低饱和冷调固化为 `Assets/Settings/InkGuofeng_PostProcess.asset`
  （ColorAdjustments saturation −22 / contrast +6 / colorFilter 0.94,0.97,1.0；WhiteBalance temperature −12 / tint +2；Tonemapping Neutral）。
  与雾无代码耦合，符合本 ADR「只做参数联调」的定位。
- C2 红线由 `HeightFogTests.InkRenderFeature_BlitSequenceUnchangedFromS1_C2RedLine` 做**源码级计数守卫**
  （去注释后 `GetTemporaryRT`×1 / `cmd.Blit(`×2 / `ReleaseTemporaryRT`×1 / `EnqueuePass(`×1 / `: ScriptableRenderPass`×1），
  任何人日后偷偷加第二条 Pass 会直接红在 CI 上。
- 回退点：`git tag s1-ink-baseline`（E1-S3 动工前的 S1 已验证墨韵栈）。
- 本 ADR 于 S2·W2 经制作人批准采纳，含上述已批准的实现偏差（keyword 声明方式偏差，裁定：接受 `multi_compile_local_fragment` 偏差）。采纳状态见文档顶部。
