# ADR-010 高度雾与墨韵 Render Feature 的集成点（E1-S3）

> 状态：**提议**
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
