# Sprint 2 QA 计划 —— 《秘境·凡尘》

> 负责人：严守真（QA / 测试）
> 版本：v1.0（初稿）
> 生成日期：2026-07-31
> 适用范围：S2 冲刺（E0-S5 存档、E2-S2 深度排序、E1-S2 国风 Toon、E1-S3 墨韵高度雾）
> 目标工程：`D:\WBzone\Game\mijing-fanchen`（Unity 2022.3.62f3c1 / URP 14.0.12）

---

## 0. 依据与「证据缺口」声明（务必先读）

### 0.1 本计划的真实依据

本文档的**全部测试点均来自对仓库磁盘实际内容的核查**，而非转述上游文档。核查时间 2026-07-31，核查基线 commit `60e4831`。

已实际读取并作为依据的产物：

| 类别 | 路径 | 核查结论 |
|---|---|---|
| 存档实现 | `Assets/Scripts/Services/SaveService.cs` `SaveCrypto.cs` `SaveData.cs` `SaveKeyProvider.cs` | 存在 |
| 深度排序 | `Assets/Scripts/Rendering/DepthSortBootstrap.cs` | 存在 |
| 墨韵栈 | `Assets/Scripts/Rendering/InkRenderFeature.cs` / `Assets/Shaders/InkFullscreen.shader` | 存在（S1 产物，E1-S3 待扩展） |
| Toon | `Assets/Shaders/ToonGuofeng.shader` + `Assets/Shaders/Include/ToonGuofengLighting.hlsl` | 存在，3 pass |
| 测试 | `Assets/Tests/EditMode/{SaveServiceTests,ToonShaderTests}.cs`、`Assets/Tests/PlayMode/DepthSortTests.cs` | 存在 |
| 测试结果 | `TestResults/editmode-results.xml` | EditMode **21/21 Passed，0 failed / 0 skipped / 0 inconclusive** |
| CI | `.github/workflows/ci.yml` | 存在，但**不含任何测试执行步骤**（见 §0.2 缺口 C） |

### 0.2 阻塞级证据缺口（需主理人裁决，本计划相应章节标注为「推定」）

派单时假定「只有 QA 计划一个文件缺失」。**磁盘核查结论：假定不成立。** 实际缺口如下：

- **缺口 A —— 上游依据文档整体不存在。**
  `production/` 与 `docs/` 两棵目录树在磁盘与 git 索引中**均不存在**。
  验证：`git ls-files` 顶层仅 `Assets/ Packages/ ProjectSettings/ .github/ README.md`；
  全盘 `find . -name "*.md"`（排除 `Library/`）**仅返回 `README.md` 一个文件**。
  因此 `production/sprints/sprint-02-plan.md`（§1 验收标准 / §4 风险 / §5 试玩窗口）与
  `docs/architecture/adr-007~010` **均无法读取**。
  → **影响**：§1 的验收标准、§5 的 S2-R1~R8 风险条目，我**无法引用原文**。
  凡属此类内容，本文档一律标注 **`[推定·待校对]`**，依据是代码注释中残留的 ADR 编号与红线描述
  （如 `ToonShaderTests.cs` 提到「ADR-008 / R5 红线」、`DepthSortTests.cs` 提到「ADR-009」、
  `ToonGuofeng.shader:12` 提到「ADR-010」、`ToonGuofengLighting.hlsl:70` 提到「ADR-010 v2 / E1-S3」）。
  **我不臆造验收点**——上游文档补齐后必须逐条校对本文档 §1 与 §5。

- **缺口 B —— `s1-ink-baseline` tag 不存在。**
  验证：`git tag -l` 返回**空**（仓库当前零 tag）。
  → 派单要求「回归基线含 `s1-ink-baseline` tag」，但该 tag 尚未创建。本计划 §4 将其列为
  **前置动作 GATE-0**，必须在 E1-S3 动工**之前**补打，否则「关雾墨韵旧基线逐像素不变」无参照物，
  该验收项直接失效。

- **缺口 C —— CI 当前根本不跑测试。**
  验证：`ci.yml` 唯一实质步骤为
  `Start-Process Unity.exe -ArgumentList "-batchmode -quit -nographics -projectPath ... -logFile ..."`，
  **无 `-runTests`、无 `-testPlatform`、无 `-testResults`**。
  注释自述「门禁（骨架，待 E1-S1 接实）：墨韵回归 + 帧率冒烟」——即门禁至今是**骨架**。
  → **影响**：§2 描述的「CI 门控」目前**尚未存在**，属本 Sprint 必须交付的工程项，
  而非既有能力。已在 §2.4 列为 `CI-TASK-1/2/3` 交付项，需程基岩配合。

- **缺口 D（对派单的技术更正）—— PlayMode 测试不需要 GPU。**
  派单称「PlayMode 需本机 GPU」。核查 `Assets/Tests/PlayMode/DepthSortTests.cs` 全文（106 行）后：
  4 个用例（3× `[UnityTest]` + 1× `[Test]`）**全部是相机状态断言与反射断言**
  （`cam.transparencySortMode`、`cam.transparencySortAxis`、`typeof(DepthSortBootstrap).GetMethod("Update")`），
  **无一处读取渲染输出、无一处截图、无一处 `Texture2D.ReadPixels`**。文件头注释亦自述
  「纯相机状态断言，不依赖渲染输出，CI 可跑」。
  → **结论：这 4 个 PlayMode 用例可在 `-nographics` 无头下执行**，应纳入 CI 门控（见 §2.2），
  不应被划入「真机项」而放弃自动化。真正需要 GPU 的是**截图基线比对**与 **FPS 实测**，与测试平台
  （EditMode/PlayMode）是**正交的两个维度**——本计划 §1 矩阵按「是否需 GPU」独立成列，
  避免沿用「PlayMode == 需真机」这一错误等价。

---

## 1. 测试矩阵（验收标准 → 测试用例）

**列义说明**：
- **平台**：EditMode / PlayMode / 手动。
- **无头可验**：能否在 `-batchmode -nographics` 下执行（决定能否进 CI 门控）。
- **需 GPU**：是否必须在 GTX 950M 真机带图形环境下执行。
- 注意「PlayMode」与「需 GPU」**互相独立**（见 §0.2 缺口 D）。

### 1.1 E0-S5 存档（SaveService / AES-256-CBC + HMAC-SHA256）

依据：`SaveServiceTests.cs` 实测用例（17 个，全部 Passed）。

| # | 验收标准 | 测试用例（实存方法名） | 平台 | 无头可验 | 需 GPU | 现状 |
|---|---|---|---|---|---|---|
| A1 | 存读往返所有字段一致 | `RoundTrip_AllFieldsEqual` | EditMode | ✅ | ❌ | PASS |
| A2 | 反篡改：任意区段翻转 1 字节必拒读且不抛未捕获异常 | `Tamper_FlipOneByte_IsRejected`（分区段参数化） | EditMode | ✅ | ❌ | PASS |
| A3 | 篡改后回退 `.bak` | `Tamper_WithBackup_FallsBackToBak` | EditMode | ✅ | ❌ | PASS |
| A4 | migration 骨架 v0 → v1 | `Migration_V0Save_UpgradesToCurrentVersion` | EditMode | ✅ | ❌ | PASS |
| A5 | 未来版本档拒读 | `Migration_FutureVersion_IsRejected` | EditMode | ✅ | ❌ | PASS |
| A6 | dev 密钥档「警告不拒读」（P3） | `DevKeySave_LoadWithReleaseService_WarnsButLoads` | EditMode | ✅ | ❌ | PASS |
| A7 | 原子写盘：不留 `.tmp`，正确轮转 `.bak` | `Save_LeavesNoTmpFile_AndRotatesBak` | EditMode | ✅ | ❌ | PASS |
| A8 | 槽位不存在返回 NotFound | `Load_MissingSlot_ReturnsNotFound` | EditMode | ✅ | ❌ | PASS |
| A9 | 截断/空文件健壮性 | `CorruptLength_IsRejectedWithoutThrow`（参数化） | EditMode | ✅ | ❌ | PASS |

**QA 判定：E0-S5 覆盖 ADEQUATE。** 断言覆盖正常路径 + 篡改 + 截断 + 版本迁移 + 原子性，边缘情况齐备。

**补充缺口（建议 S2 内补，非阻塞）**：
- A10 `[推定·待校对]` 并发/重入写同一槽位的行为未见覆盖。
- A11 磁盘写满 / 只读目录下 `Save` 的失败路径未见覆盖（可用只读目录模拟）。

### 1.2 E2-S2 深度排序（DepthSortBootstrap / ADR-009）

依据：`DepthSortTests.cs` 实测用例（4 个）。

| # | 验收标准 | 测试用例 | 平台 | 无头可验 | 需 GPU | 现状 |
|---|---|---|---|---|---|---|
| B1 | 相机置 `CustomAxis`，轴 = `-offset.normalized`（默认 offset (0,14,14) → \|y\|≈\|z\|≈0.7071，模长 1） | `Bootstrap_SetsCustomAxis_FromDefaultOffset` | PlayMode | ✅ | ❌ | 待跑 |
| B2 | 轴随 `CameraRig.offset` 推导，不写死 | `Bootstrap_AxisFollowsRigOffset` | PlayMode | ✅ | ❌ | 待跑 |
| B3 | `GreyboxBuilder.BuildScene()` 自动给主相机接线 | `GreyboxBuilder_WiresBootstrapOnMainCamera` | PlayMode | ✅ | ❌ | 待跑 |
| B4 | 零每帧成本红线：无 `Update/LateUpdate/FixedUpdate` | `Bootstrap_HasNoPerFrameCallbacks` | PlayMode（`[Test]`） | ✅ | ❌ | 待跑 |
| B5 | 排序正确性肉眼终验（SortingReview 场景，C4） | 手动：`SortingReviewBuilder` 搭场景，人工判定前后遮挡 | 手动 | ❌ | ✅ | 待做 |

**注**：B1~B4 **无头可跑**（§0.2 缺口 D），必须进 CI。B5 是唯一真机项。
**轴符号争议已在代码内解决**：`DepthSortTests.cs:9-12` 记录 ADR-009 正文 `-offset.normalized` 为准，
其代码示例 `(0,1,1)` 为符号笔误。`[推定·待校对]` ——ADR-009 原文不可读，此结论源自测试文件注释，
需 ADR 补齐后确认，并建议**回写修正 ADR-009 示例**，避免后人再踩。

### 1.3 E1-S2 国风 Toon（ToonGuofeng.shader / ADR-008）

依据：`ToonShaderTests.cs`（4 个，全部 Passed）+ shader 实际结构核查。

| # | 验收标准 | 测试用例 | 平台 | 无头可验 | 需 GPU | 现状 |
|---|---|---|---|---|---|---|
| C1 | Shader 存在且当前平台受支持 | `Shader_Exists_AndSupported` | EditMode | ✅ | ❌ | PASS |
| C2 | 编译零错误 | `Shader_CompilesWithoutErrors`（`ShaderUtil.ShaderHasError`） | EditMode | ✅ | ❌ | PASS |
| C3 | **R5 红线**：材质零描边参数（属性名不得含 `outline`） | `Shader_HasNoOutlineProperties_R5RedLine` | EditMode | ✅ | ❌ | PASS |
| C4 | 三 pass 结构（`ToonGuofengForward` / `ShadowCaster` / `DepthOnly`，墨线 Sobel 依赖 DepthOnly） | `Shader_HasForwardShadowCasterDepthOnlyPasses`（`passCount ≥ 3`） | EditMode | ✅ | ❌ | PASS |
| C5 | SRP Batcher 兼容 | 手动：Frame Debugger 查看 `SRP Batch` 合批，或 Inspector 顶部 SRP Batcher 状态栏 | 手动 | ❌ | ✅ | 待做 |
| C6 | 变体总数 ≤ 64 | 手动：`Shader Inspector → Compile and show code → variant count` | 手动 | ⚠️ 部分 | ✅ | 待做 |
| C7 | H3 Toon 视觉初评（明暗二值交界、水墨冷灰阴影、Rim 受光侧） | 手动：真机 SortingReview / Greybox 场景目视 | 手动 | ❌ | ✅ | 待做 |
| C8 | 截图基线建立 | 见 §2.3 | 手动+PlayMode | ❌ | ✅ | 待做 |

**已核实的 shader 结构**（`ToonGuofeng.shader`）：
`Pass "ToonGuofengForward"`(L100-102, LightMode=UniversalForward) /
`Pass "ShadowCaster"`(L212-215) / `Pass "DepthOnly"`(L282-285) —— 与 C4 断言一致。
属性块含 Base / Ramp / Ink Shadow / Rim / Brush / Specular 六组，**确无 outline 属性**，C3 红线成立。

**C6 的自动化建议（新增用例）**：
`ShaderUtil.GetShaderVariantCount` / `ShaderUtil.GetShaderGlobalKeywords` 属编辑器 API，
**可在 EditMode 无头下断言变体上限**，无需真机。建议新增
`Shader_VariantCount_WithinBudget`，把 C6 从「手动」升级为「无头可验」，降低人工负担。

**已知 shader keyword**（影响变体数，来自属性块）：
`_RAMPTEX_ON`、`_BRUSHNORMAL_ON`、`_SpecularOn`（Toggle）。

### 1.4 E1-S3 墨韵高度雾（InkRenderFeature / InkFullscreen.shader 扩展 · ADR-010）

**状态：代码尚未落地**，本节为**前瞻测试设计**（Story 完成前请勿据此判定 PASS/FAIL）。

已核实的关键事实（决定测试形态）：
- `ToonGuofeng.shader:12` 注释：「刻意**不加** `multi_compile_fog`：雾由墨韵全屏 Pass 负责（ADR-010）」。
- `ToonGuofengLighting.hlsl:70-76`：`ApplyMJHeightFog(color, positionWS)` **当前是恒等函数**
  （`return color;`），注释自述「ADR-010 v2 备份路径，S2 默认 no-op……**不要**在此实现雾」。
- `ToonGuofeng.shader:204` 已调用该钩子。
→ **因此 E1-S3 的雾必须实现在 `InkFullscreen.shader` 全屏 Pass 内**，
  **不得**改动 `ApplyMJHeightFog`。这构成一条可自动化的红线（下表 D1）。

| # | 验收标准 `[推定·待校对]` | 测试用例 | 平台 | 无头可验 | 需 GPU |
|---|---|---|---|---|---|
| D1 | **红线**：`ApplyMJHeightFog` 保持恒等（雾不得实现在 Toon 内） | 新增 EditMode：正则断言 `ToonGuofengLighting.hlsl` 中该函数体仅 `return color;` | EditMode | ✅ | ❌ |
| D2 | **红线**：Toon shader 不得出现 `multi_compile_fog` | 新增 EditMode：源码正则断言 | EditMode | ✅ | ❌ |
| D3 | `InkFullscreen.shader` 新增雾参数且编译零错误 | 扩展 `ToonShaderTests` 模式：`ShaderUtil.ShaderHasError("Custom/InkFullscreen")` | EditMode | ✅ | ❌ |
| D4 | `InkRenderFeature` 雾开关默认值与序列化字段存在 | 新增 EditMode：反射断言 `InkSettings` 字段名/`Range` 特性 | EditMode | ✅ | ❌ |
| D5 | **关雾时输出与 S1 墨韵基线逐像素不变** | 截图比对 vs `s1-ink-baseline` | 手动/PlayMode | ❌ | ✅ |
| D6 | 开雾时高度雾沿 Y 轴梯度正确、近处不糊 | 目视 + 截图基线 | 手动 | ❌ | ✅ |
| D7 | 开/关雾 FPS 均 ≥ 58 @1080p | `FpsProbe` 实测 | 手动 | ❌ | ✅ |

**D1/D2 价值说明**：这两条把「架构约束」变成**无头可验的自动化红线**，
与 C3（R5 零描边）同属「防止职责漂移」的守卫测试，成本极低、回归价值极高。强烈建议纳入。

**现有 `InkFullscreen.shader` 属性基线**（E1-S3 只应**新增**，不应删改，供 D3 回归比对）：
`_SourceTex` / `_LineThickness` / `_LineStrength` / `_PaperStrength` / `_FeibaiThreshold` / `_InkStainStrength`。

---

## 2. 烟雾测试清单（CI 门控）

### 2.1 门控总原则

- 烟雾门控 **FAIL 即「未达 QA」**，不放行合并。
- 门控必须**全自动、无人值守、无 GPU 依赖**。凡需 GPU 的项一律**不进**烟雾门控，
  改走 §3 真机验证（人工签收）。
- 单次门控目标耗时 ≤ 15 分钟（8GB 机器现实约束）。

### 2.2 第一层：EditMode + PlayMode 无头自动测试（CI 强制）

```powershell
# EditMode（现有 21 用例）
Unity.exe -batchmode -quit -nographics `
  -projectPath "<repo>" `
  -runTests -testPlatform EditMode `
  -testResults "<repo>\TestResults\editmode-results.xml" `
  -logFile "D:\ci_editmode.log"

# PlayMode（现有 4 用例，无 GPU 依赖 —— 见 §0.2 缺口 D）
Unity.exe -batchmode -quit -nographics `
  -projectPath "<repo>" `
  -runTests -testPlatform PlayMode `
  -testResults "<repo>\TestResults\playmode-results.xml" `
  -logFile "D:\ci_playmode.log"
```

**通过判据**：两份 XML 根节点均满足 `failed="0"` 且 `inconclusive="0"`，
且 `passed == total`（当前基线：EditMode `total=21 passed=21`）。
**`skipped > 0` 视为 CONCERNS**，需在 PR 说明中解释原因，不自动放行。

**注意（Unity 退出码陷阱）**：`ci.yml` 已记录两条铁律，测试步骤同样适用——
① `run:` 块必须 100% 纯 ASCII（PS 5.1 按 GBK 误读 UTF-8 临时脚本 → ParseError 秒退）；
② 必须 `Start-Process -Wait` 启动（`Unity.exe` PE Subsystem=2 为 GUI 子系统，
PowerShell `&` 不阻塞 → 秒退 + 取到垃圾退出码）。
**新增第三条**：`-runTests` 下 Unity 用**退出码表达测试结果**（0=全过，2=有失败，3=运行失败），
不可简单 `if ($code -ne 0) { throw }` 了事，须区分 2 与 3 并**优先解析 XML**，
否则「测试失败」与「Unity 崩了」无法区分，排障会走弯路。

### 2.3 第二层：PlayMode 截图基线比对（**本机 CI 补跑，不进无头门控**）

**前提：此层必须带图形环境（去掉 `-nographics`）**，因此**不能**在当前无头 CI 中执行。
标注为 `LOCAL-CI`：由制作人在本机带 GPU 的 runner 会话中触发。

基线文件规范：

| Story | 基线文件 | 采集场景/条件 | 容差 |
|---|---|---|---|
| E2-S2 | `Tests/Baselines/e2-s2-sorting_1920x1080.png` | `SortingReviewBuilder` 场景，固定相机 offset (0,14,14)，固定帧（`Time.captureFramerate` 锁定后取第 N 帧） | ≥ 99% 像素通道差 < 2/255 |
| E1-S2 | `Tests/Baselines/e1-s2-toon_1920x1080.png` | Greybox 场景，Toon 材质样球 + 主平行光固定角度，**墨韵 Pass 关闭**（隔离 Toon 变量） | ≥ 99% 像素通道差 < 2/255 |
| E1-S3（关雾） | `Tests/Baselines/s1-ink-baseline_1920x1080.png` | 同 S1 墨韵采集条件，雾开关 = OFF | **逐像素严格相等**（差 = 0），见下 |
| E1-S3（开雾） | `Tests/Baselines/e1-s3-fog-on_1920x1080.png` | 同上，雾开关 = ON，雾参数取默认值 | ≥ 99% 像素通道差 < 2/255 |

**关于 E1-S3 关雾项容差的 QA 立场（与派单不同，请裁决）**：
派单给的是统一容差「≥99% 像素差 <2/255」。但 D5 的语义是**「关雾 = 走原路径，不应有任何改变」**，
这是**布尔性质**而非「视觉近似」性质。若给 1% 像素的宽容，正好会**放过**「雾代码在关闭时仍轻微
污染输出」这类最该抓的回归。
→ **建议：D5 采用逐像素严格相等（diff == 0）**，其余三项沿用 99%/2-255 容差。
若关雾路径因浮点重排无法做到严格相等，则说明**关雾并未真正短路**，那本身就是应修的缺陷。
**此为建议，最终由主理人/制作人裁定。**

**采集纪律（否则基线必然 flaky）**：固定分辨率 1920×1080、固定 Quality 等级、
固定随机种子、锁定 `Application.targetFrameRate` 与 `Time.captureFramerate`、
等待 shader 编译与资源加载完成后再截图（至少 `yield return new WaitForEndOfFrame()` ×3）、
**禁用 FpsProbe 的 OnGUI 叠加**（否则 FPS 数字每帧变化会直接毁掉基线 —— 这是本工程最现实的 flaky 源，
`FpsProbe.cs` 由 `GreyboxBuilder` 自动挂到 Main Camera 上，采集前必须显式关闭）。

### 2.4 CI 交付项（当前不存在，需程基岩配合实现）

| ID | 交付内容 | 优先级 |
|---|---|---|
| `CI-TASK-1` | 在 `ci.yml` 追加 EditMode `-runTests` step + XML 结果解析 + 退出码 0/2/3 分流 | P0 |
| `CI-TASK-2` | 追加 PlayMode `-runTests` step（无头，§0.2 缺口 D 已证可行） | P0 |
| `CI-TASK-3` | 结果 XML 打印摘要到 Actions 控制台（弱网 runner 不可用 `upload-artifact`，已实测超时） | P1 |
| `CI-TASK-4` | `LOCAL-CI` 截图基线脚本（带 GPU 会话手动触发） | P1 |

**8GB 机器拆 job 建议（派单要求项）**：
当前 `ci.yml` 为单 job。加入两次 `-runTests` 后，同一 job 内会**连续三次**启动 Unity
（编译校验 + EditMode + PlayMode），Library 缓存与 Mono 堆叠加，8GB 物理内存下 OOM 风险显著上升。
→ **建议拆为两个 job**：
- `job: compile-and-editmode` —— 编译/导入校验 + EditMode 测试（复用同一次 Unity 冷启动最省内存，
  可用 `-runTests -testPlatform EditMode` 单次调用同时完成两件事，**推荐**）。
- `job: playmode`（`needs: compile-and-editmode`）—— PlayMode 测试，**串行**执行。
两 job 均 `runs-on: self-hosted` 且**必须串行**（`needs:` 保证），
**切勿并行** —— 同一台自托管机上两个 Unity 实例会争抢同一个 `Library/` 目录锁并互相踩踏，
既 OOM 又结果不可信。
另沿用 README 既有建议：Windows 页面文件置于 D 盘、放大到 16–24 GB。

---

## 3. 真机验证项（GTX 950M / Win10 / 1080p，人工签收）

**执行环境固定**：Win10 / 8GB RAM / GTX 950M / 1920×1080 全屏 / Unity 2022.3.62f3c1 / URP 14.0.12。
每项须留证据（截图或 FpsProbe 读数照片）归档至 `production/qa/evidence/s2/`。

| ID | 验证项 | 方法 | 通过判据 | 关联 |
|---|---|---|---|---|
| M1 | FPS 实测 · **关雾** | Greybox 场景运行，`FpsProbe` 读数，稳定观察 ≥ 60 秒 | FPS ≥ 58 全程 | D7 |
| M2 | FPS 实测 · **开雾** | 同上，雾开关 ON | FPS ≥ 58 全程 | D7 |
| M3 | H3 Toon 视觉初评 | Greybox/SortingReview 目视：明暗二值交界清晰、阴影呈冷灰偏青（`_ShadowTint` 默认 (0.62,0.68,0.72)）、Rim 仅受光侧、**无塑料高光**（`_SpecularOn` 默认关） | 主理人 + 美术签收「符合国风水墨调性」 | C7 |
| M4 | **关雾墨韵旧基线逐像素不变** | 与 `s1-ink-baseline` 截图比对 | 见 §2.3 容差争议，建议 diff == 0 | D5 |
| M5 | 深度排序肉眼终验 | SortingReview 场景，前后走位观察遮挡关系 | 无穿插、无闪烁（z-fighting） | B5 |
| M6 | SRP Batcher 合批确认 | Frame Debugger 查 `SRP Batch` 节点 | Toon 材质进入 SRP Batch | C5 |
| M7 | 变体数确认 | Shader Inspector → Compile and show code | ≤ 64 | C6（若 C6 自动化落地则可免） |

**M1/M2 记录要求**：须分别记录 **Draw Calls 与 Triangles**（`FpsProbe` 已通过
`ProfilerRecorder` 采集 `Draw Calls Count` / `Triangles Count`），
仅记 FPS 不足以定位「开雾掉帧」的根因。
**注意**：`FpsProbe` 的 OnGUI 叠加本身有开销，作为**一致性偏置**在开/关雾两次测量中同时存在，
故对**差值比较**无碍，但**绝对值**略偏悲观 —— 这对 ≥58 门槛是保守方向，可接受。

---

## 4. 回归基线

### 4.1 GATE-0（前置阻塞动作）—— 补打 `s1-ink-baseline`

**当前 `git tag -l` 为空，该 tag 不存在。** 必须在 E1-S3 动工前完成：

1. 确认 S1 墨韵栈最后一个「已验收」提交（候选：`7b3ef17 S1: foundation scaffold`，
   但 `b516abf fix: FpsProbe GUI context + camera-relative WASD; add URP pipeline assets`
   之后墨韵才真正可跑 —— **具体锚点须由程基岩确认**，我不替工程侧拍板）。
2. 在该 commit 上打 tag：`git tag -a s1-ink-baseline -m "S1 ink stack visual baseline"`。
3. **在该 tag 检出状态下采集截图基线** `Tests/Baselines/s1-ink-baseline_1920x1080.png`，
   按 §2.3 采集纪律执行，随后提交入库。
4. 记录采集环境指纹（GPU 驱动版本、Unity 版本、URP 版本、Quality 等级）到基线同目录
   `s1-ink-baseline.meta.txt`。**驱动更新会导致像素级差异**，无指纹则日后无法判定「是回归还是环境变了」。

**GATE-0 未完成 ⇒ D5/M4 无法执行 ⇒ E1-S3 不得签收。**

### 4.2 回归套件构成

| 层级 | 内容 | 触发时机 |
|---|---|---|
| L1 代码回归 | EditMode 21 用例 + PlayMode 4 用例（无头） | 每次 push / PR |
| L2 架构红线回归 | C3（R5 零描边）、B4（零每帧成本）、D1/D2（雾职责归属） | 每次 push（属 L1 子集，单独标注因其为「防漂移」性质） |
| L3 视觉回归 | §2.3 四张截图基线比对 | 每次涉及 shader/渲染的 PR，`LOCAL-CI` 手动触发 |
| L4 性能回归 | M1/M2 FPS + Draw Calls + Triangles | 每 Story 完成时 + Sprint 末 |

### 4.3 基线更新规则

视觉基线**只能因「有意的视觉变更」而更新**，且须：
① PR 中附「旧基线 / 新基线 / diff 图」三联；
② 主理人 + 美术明确签字；
③ 更新 `*.meta.txt` 环境指纹。
**严禁**因「测试一直红」而静默覆盖基线 —— 这是视觉回归体系最常见的失效方式。

### 4.4 已修 Bug 的回归补测

S2 内每修一个 Bug，**必须同时提交一个能复现该 Bug 的测试**（红→绿），
否则该修复不计入「完成」。当前 `production/qa/bugs/` 目录不存在，
建议随本计划一并建立，Bug 编号格式 `S2-BUG-nnn`。

---

## 5. S2-R1~R8 风险验证落点

> **`[推定·待校对]` 全节**：`sprint-02-plan.md §4` 不可读（§0.2 缺口 A），
> **S2-R1~R8 的原始定义我无从引用**。下表是我依据**代码中实际存在的风险信号**
> （注释里的红线、兼容性声明、硬件约束）反推的风险清单，编号为**占位**。
> 上游文档补齐后**必须逐条比对并重新编号**，不可直接采信本表编号映射。

| 占位编号 | 推定风险（依据） | 验证落点 | 验证类型 |
|---|---|---|---|
| S2-R1 | **URP 版本漂移**：URP 14.0.12 被误升到 v17 / Unity 6 导致墨韵栈整体崩（`InkRenderFeature.cs` 头部大段单路径兼容声明；README「已钉死，禁止升级」） | 新增 EditMode 断言 `Packages/packages-lock.json` 中 URP 版本 == `14.0.12`；CI 编译校验兜底 | 无头自动 |
| S2-R2 | **描边职责漂移**：Toon 里偷偷加描边，与墨韵 Pass 重复（R5 红线） | C3 `Shader_HasNoOutlineProperties_R5RedLine` | 无头自动（已有） |
| S2-R3 | **雾职责漂移**：雾实现进 Toon 而非墨韵全屏 Pass（`ToonGuofeng.shader:12`、`ToonGuofengLighting.hlsl:70`） | D1 + D2 新增红线测试 | 无头自动（待建） |
| S2-R4 | **深度排序轴符号错误**：ADR-009 示例 `(0,1,1)` 与正文 `-offset.normalized` 矛盾（`DepthSortTests.cs:9-12`） | B1/B2 断言 + B5 肉眼终验；并回写修正 ADR-009 | 无头自动 + 真机 |
| S2-R5 | **950M 性能不达标**：开雾后掉出 58 FPS | M1/M2（含 Draw Calls/Triangles 对比） | 真机 |
| S2-R6 | **8GB 机器 CI OOM**：多次 Unity 启动叠加爆内存 | §2.4 拆 job + 串行 + 页面文件 16–24GB；观察 CI 连续 10 次绿 | CI 观测 |
| S2-R7 | **CI 假绿**：Unity GUI 子系统退出码陷阱 / `-runTests` 退出码 2 vs 3 未分流，测试失败被当成功（`ci.yml` 已记录两条铁律） | §2.2 退出码分流 + **强制解析 XML**；故意注入一个失败用例做**门控自检**（验证门控真的会红） | CI 自检 |
| S2-R8 | **视觉基线 flaky**：FpsProbe OnGUI 叠加 / 未等 shader 编译完 / 分辨率漂移导致截图比对随机红 | §2.3 采集纪律；新基线须**连续 3 次采集互相 diff == 0** 方可入库 | LOCAL-CI |

**特别强调 S2-R7 的门控自检**：门禁至今是骨架（§0.2 缺口 C），
首次接实后**必须故意让一个用例失败**，确认 CI 真的变红。
未经自检的门控等同于没有门控 —— 这是本 Sprint 最高优先级的 QA 动作。

---

## 6. 已知环境限制

### 6.1 沙箱 / 无头环境限制

| 限制 | 影响范围 | 处置 |
|---|---|---|
| 当前 CI 为 `-batchmode -nographics`，**无 GPU 上下文** | 所有截图类测试（§2.3 全部四张基线）、FPS 实测、Frame Debugger、SRP Batcher 查看 | **必须本机带图形会话补跑**，标注 `LOCAL-CI`；不得进无头门控 |
| `-nographics` 下部分渲染 API 返回空/默认值 | 任何 `Camera.Render()` + `ReadPixels` 组合 | 禁止在无头用例中使用；若误用会**静默返回全黑图**而非报错，极具欺骗性 |
| `ShaderUtil.*` 属 `UnityEditor` 命名空间 | C1~C4、C6、D3 | **仅能在 EditMode 跑**（`MJ.Tests.EditMode.asmdef` 已正确设 `includePlatforms: ["Editor"]` 并引用 `UnityEditor.TestRunner`），不可移入 PlayMode |
| PlayMode 用例**不受**无 GPU 限制 | B1~B4 | 见 §0.2 缺口 D，应进 CI |

### 6.2 硬件限制

- **8GB 物理内存**：CI 拆两 job 串行（§2.4）；页面文件置 D 盘 16–24GB。
- **GTX 950M**：性能契约上限「1–3 盏附加点光」（`ToonGuofengLighting.hlsl:63` 注释「950M 上限 1–3 盏点光，性能契约§5」）。
  FPS 测试场景**须固定灯光数量**，否则 M1/M2 不可比。
- **弱网 runner**：已实测 `game-ci/unity-builder` 与 `actions/upload-artifact@v4` 均 100s 超时 ×3 失败。
  → 测试结果**只能打印到控制台 + 落盘 `D:\`**，**不得**引入新的 Marketplace action（`CI-TASK-3`）。

### 6.3 测试稳定性（flaky）纪律

- 任何用例连续 3 次运行中出现 ≥1 次结果不一致，即判定为 **flaky**，
  **立即用 `[Ignore("S2-BUG-nnn flaky")]` 隔离并开 Bug**，不允许留在门控里污染 CI 信号。
- 隔离的用例必须挂 Bug 号并在 Sprint 末复盘，**禁止长期挂起**。
- 当前已识别的最高 flaky 风险源：**`FpsProbe` 的 OnGUI 每帧变化数字**（§2.3）与
  **shader 首次编译耗时**（截图早于编译完成 → 粉色/黑色画面）。

---

## 7. S2 质量门初步判定

| 维度 | 判定 | 理由 |
|---|---|---|
| E0-S5 存档 | **PASS** | 17/17 通过，覆盖 ADEQUATE（正常/篡改/截断/迁移/原子性） |
| E1-S2 Toon（无头部分） | **PASS** | 4/4 通过，含 R5 红线守卫；C5~C7 真机项待做 |
| E2-S2 深度排序 | **CONCERNS** | 用例质量好且无头可跑，但**尚未在 CI 中执行过**（无 `playmode-results.xml`），B5 真机终验未做 |
| E1-S3 墨韵雾 | **N/A** | 代码未落地 |
| **CI 门控体系** | **FAIL** | `ci.yml` **无任何 `-runTests`**，门禁仍是骨架；`s1-ink-baseline` tag 不存在 |
| **文档基线** | **FAIL** | `production/` 与 `docs/` 整棵树缺失，验收标准与 ADR 不可追溯 |

### 综合判定：**CONCERNS（趋向 FAIL，取决于两项阻塞能否在 S2 内清掉）**

**代码与测试本身质量是好的**——21/21 绿、断言扎实、红线守卫思路正确。
问题**不在代码，在于「验证体系」尚未闭环**：

**两个 FAIL 必须清掉，否则 S2 不建议签收：**
1. **CI 接实测试执行**（`CI-TASK-1/2`）+ **门控自检**（S2-R7）。
   现状是「测试写了但 CI 不跑」，等于**靠人自觉**，一次遗忘就前功尽弃。
2. **补打 `s1-ink-baseline` tag 并采集基线截图**（GATE-0）。
   不做这一步，E1-S3 的核心验收项 D5/M4 **物理上无法执行**。

**文档缺口（缺口 A）**属主理人裁决范围：可接受「S2 内补齐」，
但**在补齐前，本计划 §1 与 §5 中所有 `[推定·待校对]` 项不得作为正式验收依据**。

---

## 8. 待主理人审批 / 裁决事项

1. **[裁决] D5 容差**：关雾墨韵基线用「逐像素严格相等」（QA 建议）还是派单原定的「≥99% 像素差 <2/255」？（§2.3）
2. **[裁决] 缺口 A**：`production/` 与 `docs/` 整树缺失，是「本就未产出」还是「丢失需恢复」？S2 内是否补齐？
3. **[指派] GATE-0**：`s1-ink-baseline` 的锚点 commit 需程基岩确认（`7b3ef17` 还是 `b516abf`？）
4. **[指派] CI-TASK-1~4**：需程基岩实现，QA 提供判据与自检用例。
5. **[确认] S2-R1~R8**：请提供风险原文，我据以重编 §5 映射表。
6. **[建议采纳与否] 新增无头红线测试**：C6 变体数自动化、D1/D2 雾职责红线、S2-R1 URP 版本钉死断言 —— 三项成本低、回归价值高。

---

*本计划所有结论可追溯至仓库实际文件与 `TestResults/editmode-results.xml`。
凡标注 `[推定·待校对]` 者均因上游文档不可读，需补齐后校对，QA 不臆造验收点。*
