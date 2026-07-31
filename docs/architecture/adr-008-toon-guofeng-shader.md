# ADR-008 国风 Toon 着色器架构（E1-S2）

> 状态：**提议**（技术骨架先行；视觉参数待 art-director 对齐后调参，不改架构）
> 关联：ADR-002（单条 Ink 全屏 Pass）、ADR-003（资产/笔触法线规范）、C2/C5（单 Pass、跨版本单路径）、R5（双描边脏化）
> 引擎钉定：Unity 2022.3.62 · URP **14.0.12**（`Packages/manifest.json` 实钉值；README 中 14.0.6 为旧文案，需修正）

## 上下文

- 灰盒期没有国风 Toon 着色器（S3 最小版缺失），角色/场景目前走 URP Lit，观感与墨韵全屏 Pass 不统一。
- 墨韵栈已负责**屏幕空间勾线**（深度 Sobel）；Toon 着色器**绝不能再做几何描边**（Inverted Hull / 法线外扩），否则双描边脏化（R5）。
- 目标机 GTX950M：fill-rate 与带宽敏感，变体数量与贴图采样次数都要省。
- 需在 CI 无头环境可验证：着色器编译零错误可无头验证；观感验收（H3）必须真机截图。

## 备选方案

1. **Shader Graph**——迭代快、TA 友好；但生成代码不可控、变体膨胀难管、URP 14 的 Graph 在自定义光照（ramp 分档）上需要 Custom Function 绕行，且与墨韵栈的手写 HLSL 维护习惯割裂。⚠️
2. **手写 HLSL（URP ShaderLab，经典 includes）**——与 `InkFullscreen.shader` 同栈同习惯；SRP Batcher 兼容可控（`CBUFFER UnityPerMaterial`）；变体靠 `shader_feature_local` 精确管理；对 950M 可手动裁剪指令。✅
3. **第三方 Toon 资产（如 lilToon/UTS）**——功能全但体量大、许可与升级路径不可控，违背小仓库可审计原则。❌

## 决定

**手写 HLSL**：新建 `Assets/Shaders/ToonGuofeng.shader`（+ 可复用的 `ToonGuofengLighting.hlsl` include）。

### 光照与风格结构（技术骨架，参数值全部暴露给美术）

| 层 | 实现 | 说明 |
|----|------|------|
| 主光 ramp | `smoothstep` 双阈值分档（2–3 档）为默认；`_RampTex` 1D LUT 为可选开关 | 分档数/软硬度是美术参数 |
| 阴影 | 接收 URP 主光阴影，阴影色走**冷灰偏青的水墨阴影色** `_ShadowTint`（乘色而非压黑） | 国风“淡墨”感的关键 |
| 附加光 | Forward+ 附加光简化为半兰伯特 × ramp 一档 | 950M 上限制场景 1–3 盏实时点光（性能契约§5） |
| Rim | 菲涅尔 × 主光方向掩码（只亮受光侧），`_RimColor/_RimPower` | 勾勒剪影，替代高光 |
| 笔触法线 | `_BrushNormalMap`（ADR-003 authoring 规范）扰动 ramp 输入 | 让明暗交界呈毛笔干湿变化 |
| 高光 | 默认**关**（`shader_feature`）；开启时为分档色块高光 | 国风非塑料感 |
| 描边 | **无**。屏幕勾线完全交给墨韵 Ink Pass（R5 红线） | 材质里不留描边参数，杜绝误开 |
| 雾接入点 | 预留 `ApplyMJHeightFog(color, positionWS)` 空实现钩子（见 ADR-010） | S2 内高度雾走全屏 Pass，此钩子默认 no-op，仅为 v2 备份路径 |

### 工程约束

- **单 Pass 前向**（`UniversalForward`）+ `ShadowCaster` + `DepthOnly` 三个 pass，不加 `DepthNormals`（墨线只用深度 Sobel，省一遍绘制）。
- SRP Batcher 兼容：所有材质属性进 `CBUFFER_START(UnityPerMaterial)`。
- 变体预算：`shader_feature_local` ≤ 4 个（RampTex / Specular / BrushNormal / 备用），编译变体总数 ≤ 64。
- 跨版本单路径（C5）：只用 URP 14 经典 includes，不写版本宏。
- 材质模板：编辑器菜单 `MJ → Create Toon Material`（仿 `InkMaterialCreator` 模式），产 `Assets/Materials/ToonGuofeng_Default.mat`。

### 验证（CI 可跑部分）

- EditMode 测试：`Shader.Find("Custom/ToonGuofeng") != null`、`shader.isSupported`、编译无 error（`ShaderUtil.ShaderHasError`，编辑器 API）。
- 真机部分：H3 观感评审（制作人试玩窗口）+ 墨韵回归基线新增「Toon 测试球/角色胶囊」场景截图比对。

## 后果

- ✅ 与墨韵栈同一维护栈；描边职责单一（R5 闭环）；950M 指令数可控。
- ✅ 视觉参数与架构解耦：art-director 对齐只动材质参数与 Ramp LUT，不动 shader 结构。
- ⚠️ 手写 HLSL 对 TA 迭代不如 Graph 快——用「参数全暴露 + 材质模板」缓解。
- ⚠️ 若后续要求各向异性发丝/绒毛等高级效果，需追加 pass 或放弃部分（届时再开 ADR）。
