# Sprint 2 实现计划 — 《秘境·凡尘》制作阶段第一个内容冲刺

> 文档状态：工程规划（程基岩）/ 待主理人 + 制作人确认后启动实施
> 关联：`production/roadmap/sprint-plan.md` §3/§4(S2)、`production/sprints/sprint-01-plan.md`（S1 已全绿收口）、
> `../../docs/architecture/production-architecture.md`（ADR-001~006 内嵌）、
> **本冲刺新增 ADR**：`../../docs/architecture/adr-007-save-encryption.md` · `adr-008-toon-guofeng-shader.md` · `adr-009-yz-depth-sorting.md` · `adr-010-height-fog-integration.md`
> 受众：工程侧（程基岩 + AI 代码生成）+ 制作人（看 §0 概览与 §5 试玩窗口）
> 范围：**4 个 Story，合计 24 SP**（E0-S5 存档加密 8 · E1-S2 国风 Toon 8 · E2-S2 Y-Z 排序 5 · E1-S3 高度雾 3）
> 严守约束：C1 引擎锁 2022.3.62 / URP **14.0.12**（manifest 实钉值）· C2 墨韵单 Pass · C3/C4 固定 45°+Y-Z 排序 · C5 跨版本单路径 · C6/P1 反通胀

---

## 0. 冲刺概览与地基现状

**S1 已交付地基（全绿）**：玩家控制（相机相对移动）、FpsProbe、墨韵单 Pass Render Feature（`Assets/Scripts/Rendering/InkRenderFeature.cs` + `Assets/Shaders/InkFullscreen.shader`）、URP 14.0.12 管线资源、CI（自托管 runner，PowerShell 直调 Unity）绿。

**S2 主题**：「渲染起步 + 数据安全」——角色开始有国风观感（H3 评审入口）、2.5D 排序正确性落地、雾效并入墨韵栈、存档从零到「加密可信」。

| Story | 名称 | SP | ADR | 主要产出路径（`<REPO_ROOT>` = `D:\WBzone\Game\mijing-fanchen\`） |
|-------|------|----|-----|------------------------------------------------|
| E0-S5 | 存档加密（AES） | 8 | ADR-007 | `Assets/Scripts/Services/SaveService.cs` 等 |
| E1-S2 | 国风 Toon 着色器（技术骨架） | 8 | ADR-008 | `Assets/Shaders/ToonGuofeng.shader` 等 |
| E2-S2 | Y-Z 深度排序 | 5 | ADR-009 | `Assets/Scripts/Rendering/DepthSortBootstrap.cs` |
| E1-S3 | 高度雾（并入墨韵 Pass） | 3 | ADR-010 | `InkFullscreen.shader` / `InkRenderFeature.cs` 扩展 |

> **范围差异说明（需主理人确认）**：路线图原 S2 还含 **E0-S6 遥测脚手架（3 SP，FpsProbe→Telemetry）**，本次任务指令未列入。建议：若产能富余（§3 排程含缓冲）则顺带做，否则顺延 S3 并在路线图标注。
> **文档漂移修正项**：`README.md` 写 URP「14.0.6」，`Packages/manifest.json` 实钉 **14.0.12** ——S2 内顺手改 README 一行，消除口径漂移（不算 Story）。

---

## 1. 逐 Story 实现计划

### E0-S5 · 存档加密（AES）— 8 SP

**目标**：`SaveService` 版本化 JSON + AES 往返一致；反篡改校验；migration 骨架；密钥 CI 注入。
**技术方案**：见 **ADR-007**（AES-256-CBC + HMAC-SHA256 Encrypt-then-MAC；PBKDF2 派生、加密/认证密钥分离；`MJFC` 容器格式；原子写盘 + `.bak` 兜底；编辑器解密调试菜单）。
**衔接**：全新（ADR-005 细化）。纯 C#，与渲染三件事**零耦合**。

**拆分子任务**
1. `SaveData` schema v1（`saveVersion` + 玩家位置/境界占位 + 不可再生池占位字段）+ `JsonUtility` 序列化。
2. `SaveCrypto`（加密/HMAC/派生，静态纯函数，独立可测）。
3. `SaveService`（槽位管理、原子写盘、`.bak` 回退、读档验 MAC→解密→migration 链骨架 v1→v1 恒等）。
4. CI 密钥注入：ci.yml 增加由 Secret `SAVE_ROOT_SECRET` 生成 `SaveSecret.cs` 的前置步骤；编辑器 dev 密钥回退。
5. 编辑器菜单 `MJ → Save → Dump Decrypted JSON`（调试用）。

**要创建/修改的文件**
- `Assets/Scripts/Services/SaveData.cs`、`SaveCrypto.cs`、`SaveService.cs`（新建）
- `Assets/Scripts/Editor/SaveDebugMenu.cs`（新建）
- `Assets/Tests/EditMode/SaveServiceTests.cs`（新建）
- `.github/workflows/ci.yml`（修改：密钥生成步骤）、`.gitignore`（追加 `SaveSecret.cs`）

**验收标准（对照路线图 + 可验证化）**
- ✅ 往返一致：任意 `SaveData` → 加密写盘 → 读盘 → 字段全等（EditMode 测试）。
- ✅ 反篡改：密文**任意位置翻转 1 字节** → 读档拒绝且不抛未捕获异常，回退 `.bak`（参数化测试覆盖 头部/salt/IV/MAC/密文 各区段）。
- ✅ migration 骨架：`saveVersion=0` 假档 → 升级链跑通到 v1（骨架恒等升级即可）。
- ✅ 密钥不进仓库：仓库内 grep 不到密钥字面量；CI 日志不回显 Secret。
- ✅ CI 全绿：以上全部为 EditMode 测试，无头 `-batchmode -nographics -runTests` 可跑（**本 Story 是 S2 中唯一 100% 无头可验的**）。

**测试证据路径**：`<REPO_ROOT>/TestResults/editmode-results.xml`（CI artifact）+ `Assets/Tests/EditMode/SaveServiceTests.cs`。

---

### E1-S2 · 国风 Toon 着色器（技术骨架先行）— 8 SP

**目标**：`ToonGuofeng.shader` 技术骨架（Ramp 分档 + 水墨阴影色 + Rim + 笔触法线接口 + 无几何描边）；SRP Batcher 兼容；变体受控；**视觉参数留给 art-director 对齐，本冲刺不追求最终观感**。
**技术方案**：见 **ADR-008**（手写 HLSL；三 pass：Forward/ShadowCaster/DepthOnly；`shader_feature_local` ≤ 4；描边职责 100% 归墨韵 Pass，材质不留描边参数——R5 红线）。
**衔接**：全新（S3 最小版缺失）；下游 S3 战斗观感与 H3 评审依赖它。

**拆分子任务**
1. `ToonGuofengLighting.hlsl`（ramp/rim/阴影色纯函数）+ `ToonGuofeng.shader` 主体。
2. ShadowCaster / DepthOnly pass（保证墨线深度 Sobel 对 Toon 物体正常勾边）。
3. `MJ → Create Toon Material` 编辑器菜单 + 默认材质模板。
4. Toon 测试场景（球/胶囊/带笔触法线的平面 × 主光 + 1 点光），并入墨韵回归基线（新增 1 张截图基线）。
5. 编译与兼容测试（EditMode）。

**要创建/修改的文件**
- `Assets/Shaders/ToonGuofeng.shader`、`Assets/Shaders/Include/ToonGuofengLighting.hlsl`（新建）
- `Assets/Scripts/Editor/ToonMaterialCreator.cs`（新建）
- `Assets/Tests/Scenes/ToonReview.unity`、`Assets/Tests/Baseline/toon_baseline.png`（新建，LFS）
- `Assets/Tests/EditMode/ToonShaderTests.cs`（新建）

**验收标准**
- ✅ 编译零错误：`ShaderUtil.ShaderHasError == false`、`shader.isSupported == true`（EditMode，CI 可跑）。
- ✅ SRP Batcher 兼容：Frame Debugger 中 Toon 物体走 SRP Batch（真机人工确认 + 截图留档）。
- ✅ 无双描边：Toon 材质无任何描边参数；测试场景中勾线仅来自墨韵 Pass（截图基线比对）。
- ✅ 变体预算：编译变体总数 ≤ 64（`ShaderUtil` 统计留档）。
- ✅ 与墨韵共存：ToonReview 场景开墨韵 → 截图基线通过；墨韵栈耗时仍 < 3ms。
- ⏳ H3 观感：制作人试玩窗口初评（**非**本 Story 出门条件；最终观感待 art-director 参数对齐后另行评审）。

**测试证据路径**：`TestResults/editmode-results.xml` + `Assets/Tests/Baseline/toon_baseline.png` + Frame Debugger 截图（`production/sprints/evidence/s2/`）。

---

### E2-S2 · Y-Z 深度排序 — 5 SP

**目标**：斜 45° 下透明队列物体（面片/特效/组合角色）无穿插错排（C4）。
**技术方案**：见 **ADR-009**（`TransparencySortMode.CustomAxis`，轴由 `CameraRig.offset` 推导 `(0,1,1).normalized`；组合体挂 `SortingGroup`；不透明物一律走深度缓冲，禁止为排序改透明队列）。
**衔接**：全新（灰盒仅概念）；E2-S3 gizmo 可视化在 S3，不含。

**拆分子任务**
1. `DepthSortBootstrap.cs`（主相机一次性设置，轴从 CameraRig 推导，单一事实来源）。
2. `GreyboxBuilder`/场景接线：主相机自动挂载（沿用 Builder 自动装配模式）。
3. 排序验收场景：3 组前后站位透明面片 + 1 个 SortingGroup 多面片组合体。
4. PlayMode 断言 + 截图基线。
5. 控制清单条目起草：「组合体必挂 SortingGroup / 禁把不透明改 Transparent」（并入 §4 控制规则）。

**要创建/修改的文件**
- `Assets/Scripts/Rendering/DepthSortBootstrap.cs`（新建）
- `Assets/Scripts/Core/GreyboxBuilder.cs`（小改：相机装配处 +1 行挂载）
- `Assets/Tests/Scenes/SortingReview.unity`、`Assets/Tests/Baseline/sorting_baseline.png`（新建）
- `Assets/Tests/PlayMode/DepthSortTests.cs`（新建）

**验收标准**
- ✅ 相机状态断言：`transparencySortMode == CustomAxis` 且轴 ≈ `(0, 0.7071, 0.7071)`（随 offset 推导，PlayMode 测试，CI 可跑——纯状态断言不需渲染输出）。
- ✅ 无穿插：SortingReview 场景截图基线通过；肉眼复核前后站位透明面片遮挡关系正确（C4 Pass）。
- ✅ 组合体完整：SortingGroup 组合体整体前后移动无构件互穿。
- ✅ 零每帧成本：Profiler 无新增每帧脚本开销（Bootstrap 只在初始化执行）。

**测试证据路径**：`TestResults/playmode-results.xml` + `Assets/Tests/Baseline/sorting_baseline.png`。

---

### E1-S3 · 高度雾（并入墨韵全屏 Pass）— 3 SP

**目标**：世界空间高度雾（低洼墨气/高台清透），零新增 Pass、零新增 Blit，墨韵栈总耗时仍 < 3ms。
**技术方案**：见 **ADR-010**（`InkFullscreen.shader` 内新增 keyword 门控的前置雾阶段：深度重建 `positionWS.y` → 指数高度雾解析式 → 先晕染后勾线；`InkRenderFeature.InkSettings` 增 `HeightFogSettings` 子块 + clamp；禁 raymarch）。
**衔接**：扩展墨韵栈（唯一动到 S1 已验证代码的 Story，改动面刻意最小化）；Volume 色调（低饱和冷调）走 URP 内建，仅参数联调。

**拆分子任务**
1. Shader：`_MJ_HEIGHT_FOG` keyword + 世界坐标重建 + 雾混合（含天空混合上限 `_FogSkyBlend`）。
2. C#：`HeightFogSettings` 子块 + 参数 clamp + `CoreUtils.SetKeyword` 同步。
3. 回归：新增开雾截图基线 `ink_fog_baseline.png`；耗时断言口径不变（墨韵栈整体 < 3ms）。
4. Volume Profile 固化：Color Grading 低饱和冷调参数入 `Assets/Settings/`。

**要创建/修改的文件**
- `Assets/Shaders/InkFullscreen.shader`（修改：+雾阶段）
- `Assets/Scripts/Rendering/InkRenderFeature.cs`（修改：+HeightFogSettings）
- `Assets/Tests/Baseline/ink_fog_baseline.png`（新建，LFS）、`Assets/Tests/EditMode/HeightFogTests.cs`（新建）
- `Assets/Settings/`（Volume Profile 固化）

**验收标准**
- ✅ 关雾零回归：`_MJ_HEIGHT_FOG` off 时既有墨韵截图基线**逐像素不变**（变体剔除生效）。
- ✅ 开雾正确：低 Y 区域雾浓、高 Y 区域清透、天空不糊死；`ink_fog_baseline.png` 基线通过。
- ✅ 性能：墨韵栈（含雾）ProfilerRecorder < 3ms（950M 真机回填实测值，目标雾增量 < 0.5ms）。
- ✅ 参数安全：全参数越界 clamp、无 NaN（EditMode，沿用 E1-S1 ArgumentGuard 模式，CI 可跑）。
- ✅ 无新增 Pass/Blit：Frame Debugger 确认全屏 Pass 数与 S1 持平（C2 守住）。

**测试证据路径**：`TestResults/editmode-results.xml` + 两张 Ink 基线图 + Frame Debugger 截图。

---

## 2. 依赖排序与并行建议

```
E0-S5 存档加密 ────────────────────────────►│ 全程独立（纯 C#，唯一动 ci.yml 的 Story）
E2-S2 Y-Z 排序 ──────►│ 独立小件，先做先了
E1-S2 Toon 骨架 ──────────────►│ 独立，但其 DepthOnly pass 是墨线勾 Toon 物体的前提
                               └──► E1-S3 高度雾（软依赖 Toon 场景做联合验收；硬依赖仅墨韵栈本身）
```

- **可完全并行**：E0-S5 ∥（E2-S2 → E1-S2 → E1-S3）。存档与渲染三件事零共享文件，两条泳道互不阻塞。
- **渲染泳道建议串行顺序**：**E2-S2（5）→ E1-S2（8）→ E1-S3（3）**。理由：
  1. E2-S2 最小、独立、验收快，先落地让后续两个渲染 Story 的测试场景直接在「排序正确」的前提下搭；
  2. E1-S3 是**唯一修改 S1 已验证墨韵代码**的 Story，放最后——此时 Toon 测试场景已有，雾+Toon+墨线三者可一次联合验收，且若雾出问题回退不牵连其他 Story；
  3. Toon 的 DepthOnly pass 先就位，雾的深度重建与墨线 Sobel 对 Toon 物体的行为才完整可测。
- **周节奏建议（2 周冲刺）**：
  - W1：E0-S5 子任务 1–3（往返+反篡改绿）∥ E2-S2 全部 + E1-S2 子任务 1–2。
  - W2：E0-S5 子任务 4–5（CI 密钥）∥ E1-S2 子任务 3–5 → E1-S3 全部 → 回归全绿 + 试玩窗口。
  - 缓冲：约 2 天。若消耗不掉，可拾回 E0-S6 遥测（3 SP，见 §0 范围差异说明）。
- **对外依赖（非阻塞）**：art-director 的 Toon 视觉参数对齐——骨架不等它；建议 S2 末把 `ToonReview.unity` 截图包发给美术侧作为对齐输入。

## 3. Story Point 复核

| Story | 路线图原估 | 复核后 | 说明 |
|-------|-----------|--------|------|
| E0-S5 | 8 | **8** | 加密本体不难，成本在测试矩阵（篡改分区段参数化）+ CI 密钥接线 |
| E1-S2 | 8 | **8** | 手写 HLSL 三 pass + 基线设施；「骨架先行、观感后置」已把美术反复排除在外 |
| E2-S2 | 5 | **5** | 实现仅 1 脚本，SP 主要在验收场景/基线/规范落地；不下调，留验证余量 |
| E1-S3 | 3 | **3** | 得益于并入墨韵 Pass（ADR-010），无新 Feature 脚手架成本 |
| **合计** | 24 | **24** | 速率假设 30 SP/冲刺 → 留 6 SP 缓冲（或吸收 E0-S6 的 3 SP） |

## 4. 风险评估

| # | 风险 | 概率/影响 | 缓解 |
|---|------|----------|------|
| S2-R1 | **8GB 内存机器**：编辑器 + PlayMode 测试 + 截图基线比对同跑易触顶换页，CI 变慢甚至超时 | 高/中 | CI 分两个 job 串行（EditMode 无图形优先、PlayMode/截图殿后）；沿用 S1 页面文件 16–24GB 建议；基线图钉 1080p 单帧，不做多分辨率矩阵 |
| S2-R2 | **GTX950M fill-rate**：雾并入墨韵后单 Pass 指令数增加，栈耗时逼近 3ms 红线 | 中/高 | ADR-010 keyword 门控（关雾零成本）；雾为解析式无循环；若实测超线，启用既有「半分辨率」预案（E1-S6 提前）而非拆第二条 Pass |
| S2-R3 | **墨韵集成冲突点**：E1-S3 直接改 `InkFullscreen.shader`/`InkRenderFeature.cs`，是 S2 唯一可能破坏 S1 已验证行为的点 | 中/高 | 排最后做；「关雾时旧基线逐像素不变」为硬验收；改动前打 tag，回退即还原 |
| S2-R4 | 双描边复发：Toon 若被顺手加几何描边，与墨线叠加脏化（R5） | 低/中 | ADR-008 材质**不留描边参数**；截图基线含勾线区域比对 |
| S2-R5 | 截图基线脆弱：驱动/平台差异导致像素比对误报红 | 中/中 | 比对用容差阈值（如 ≥99% 像素差 < 2/255）；基线只在自托管 runner（固定机器）生成与比对 |
| S2-R6 | `JsonUtility` 表达力不足（字典/多态）导致存档 schema 返工 | 低/中 | v1 schema 刻意扁平；确需字典时按 ADR-007 升级到 Unity 官方 Newtonsoft 包（migration 链兜底旧档） |
| S2-R7 | CI 无头环境无法渲染截图（`-nographics`） | 中/中 | 渲染类断言全部放 PlayMode job，**不带 `-nographics`**（自托管 runner 有 GPU，S1 帧率冒烟已验证此路径可行）；纯逻辑断言留 EditMode 无头跑 |
| S2-R8 | AesGcm 类 API 误用（Mono 不稳） | 低/高 | ADR-007 明令 CBC+HMAC 组合，代码评审对照 |

**控制规则新增（并入控制清单/评审 checklist）**：
1. 多面片组合体必挂 `SortingGroup`；2. 禁止为排序把不透明材质改 Transparent；3. Toon 材质禁出现描边参数；4. 全屏效果只允许并入墨韵 Pass（新 Feature 须 ADR）；5. 存档字段变更必须 `saveVersion+1` 并补 upgrader + 测试。

## 5. S2 末制作人试玩窗口

**触发条件**：四 Story 验收全绿 + CI 绿。
**可试玩/可看**：灰盒场景中 Toon 材质示例体（H3 初评）、低洼处墨气高度雾、透明面片前后遮挡正确；存档为后台能力（试玩感知弱，看 CI 测试报告即可）。
**制作人要报回**：① H3 观感第一印象（供 art-director 参数对齐参考）；② 950M 上 FPS 是否仍 ≥58（H2 底线，雾开/关各看一次）；③ 雾的浓度主观感受。

## 6. 待主理人拍板

| # | 事项 | 建议 |
|---|------|------|
| P1 | ADR-007/008/009/010 四份提议状态 ADR | 批准后 S2 启动实施 |
| P2 | E0-S6 遥测（3 SP）是否纳入 S2 | 缓冲吃得下就纳入，否则顺延 S3 |
| P3 | 正式构建是否拒读 dev 密钥存档（ADR-007 遗留项） | 建议 v1 先「警告不拒读」，发行前收紧 |
| P4 | README URP 版本口径修正（14.0.6 → 14.0.12） | 顺手改，一行 |

---
*产出清单：本文件 + `../../docs/architecture/adr-007~010`；实施代码不在本任务范围（规划先行，批准后进入实现）。*
