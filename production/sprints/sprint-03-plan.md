# Sprint 3 实现计划 — 《秘境·凡尘》战斗骨架起步

> 文档状态：工程规划（程基岩）/ 待主理人 + 制作人确认后启动实施
> 关联：`production/roadmap/sprint-plan.md` §4(S3)/§5(衔接矩阵)、`production/sprints/sprint-02-plan.md`（S2 已收口）、`production/sprints/sprint-02-qa-plan.md`（QA 视角）、
> `../../docs/architecture/production-architecture.md`（ADR-001~006 内嵌）、`../../docs/architecture/adr-007~010.md`（S2 已批准，本冲刺复用）
> **本冲刺引用/待决 ADR**：ADR-006（New Input System，状态：**推荐待确认**，被 E3-S1 引用，见 §6）；ADR-007~010 复用
> 受众：工程侧（程基岩 + AI 代码生成）+ 制作人（看 **§0 概览** 与 **§5 试玩窗口**，这两节用大白话；§1~§4/§6 为工程侧）
> 范围：**6 个 Story，合计 26 SP**（E1-S4 5 · E1-S5 5 · E3-S1 8 · E2-S3 2 · E1-S6 3 · **E0-S6 3（S2 顺延）**）
> 严守约束：C1 引擎锁 2022.3.62 / URP **14.0.12** · C2 墨韵单 Pass · C3/C4 固定 45°+Y-Z 排序 · C5 跨版本单路径 · C6/P1 反通胀
> **CI 铁律（ci.yml 修改时必守）**：run 块 100% 纯 ASCII（中文仅限 `#` 注释）；启动 Unity 必用 `Start-Process -Wait`（绝不用 `&`）；`-runTests` 绝不加 `-quit`（否则假绿）
> *本文件仅规划，不写任何游戏代码、不 git commit；批准后由工程侧进入实现。*

---

## 0. 冲刺概览与收口现状

**S2 已收口事实（可放心往前推）**：

- **四 Story 全绿落地**：E0-S5 存档 AES（ADR-007）、E1-S2 国风 Toon（ADR-008）、E2-S2 Y-Z 排序（ADR-009）、E1-S3 高度雾（ADR-010），四份 ADR 均已批准。
- **CI 门控收口**：EditMode **57** / PlayMode **4** 全绿（2026-08-03 实测 `total=57 passed=57 failed=0 skipped=0`），门禁不再是骨架。
- **4 张视觉基线走 Git LFS 已落库**（S2-QA §7 结论）。
- **S2 已知遗留（不阻塞，S3 需留意）**：
  - （a）像素级 4 张基线（`ink/ink_fog/toon/sorting_baseline.png`）仍为 `.pending` 占位，需**制作人本机带 GPU 采集替换**（S2-QA §7）；本冲刺 E1-S4 写意会新增第 5 张，需一并采集。
  - （b）A11「CI 日志不回显 Secret」补测待排（低成本，可 S3 顺手补，非阻塞）。

**S3 主题：「战斗骨架起步」**（给制作人一句大白话：*这一冲刺让玩家从「能走」进化到「能打」——先把战斗控制的骨架搭起来，数值先占位、后面再慢慢调手感；渲染侧把施法笔触、合批、半分辨率守门补齐，把帧率和 DrawCall 这两个性能命门守住；顺带把 S2 没来得及做的遥测脚手架补上。*）

| Story | 名称 | SP | ADR / 性质 | 主要产出路径（`<REPO_ROOT>` = `D:\WBzone\Game\mijing-fanchen\`） |
|-------|------|----|-----------|------------------------------------------------|
| E1-S4 | 法术写意笔触 | 5 | 全新（扩展墨韵栈，复用 ADR-002/C2） | `Assets/Shaders/InkFullscreen.shader` 等 |
| E1-S5 | 合批与 LOD 守门 | 5 | 全新（守性能预算 §5 DrawCall<2000） | `Assets/Scripts/Rendering/BatchProbe.cs` |
| E3-S1 | 玩家控制重写 | 8 | 全新（重构 PlayerController；ADR-006 待确认；**状态机设计见 design GDD**） | `Assets/Scripts/Combat/PlayerCombatController.cs` |
| E2-S3 | 排序轴调试可视化 | 2 | 全新（依赖 E2-S2 已落地） | `Assets/Scripts/Editor/DepthSortGizmo.cs` |
| E1-S6 | 墨韵半分辨率 + 预算守门 | 3 | 全新（扩展墨韵栈，复用 S2-R2 预案） | `Assets/Shaders/InkFullscreen.shader` 等 |
| **E0-S6** | **遥测脚手架**（**S2 顺延**） | **3** | 全新（演进 FpsProbe→Telemetry） | `Assets/Scripts/Services/Telemetry.cs` |
| **合计** | | **26** | 速率假设 30 → 留 **4 SP** 缓冲 | |

> **范围差异说明（需主理人确认，见 §6 P1/P4）**：路线图 `sprint-plan.md` §4 的 **S2 行仍列 E0-S6（27 SP）**、**S3 行仅 23 SP**。但 `sprint-02-plan.md` §0 已明确把 E0-S6 从 S2 **顺延进 S3**，S2 实际收口为 24 SP（4 Story）。本计划按「S3 含 E0-S6」计 **26 SP**。路线图 §4 的 S2/S3 数字与现实存在口径漂移，建议同步修正（详见 §6 P4 与文末冲突说明）。

---

## 1. 逐 Story 实现计划

### E1-S4 · 法术写意笔触 — 5 SP

**目标**：施法走 Ink Pass 一笔写意（概念 H 清单 5）；Bloom 仅法宝辉光；复用 C2 单 Pass 约束，不新增独立 Pass；关写意时旧墨韵基线逐像素不变。
**技术方案**：**全新**——扩展 `InkFullscreen.shader` / `InkRenderFeature`（复用 ADR-002 单 Pass + S1 已验证的 CommandBuffer 单路径；写意笔触作墨韵栈内新增阶段，`keyword` 门控，关时零成本，沿用 ADR-010「关即逐像素不变」硬验收范式）。
**衔接**：扩展 S1 墨韵栈（与 E1-S3 同性质，是 S3 中第二个动 S1 已验证代码的 Story），改动面最小化；不引入新 Render Feature。

**拆分子任务**
1. Shader：`_MJ_XIEYI_STROKE` keyword + 施法轨迹采样（屏幕空间一笔晕染，借用既有纸纹/飞白噪声）+ 法宝辉光 Bloom 接口（仅辉光蒙版，不污染全屏）。
2. C#：`InkRenderFeature.InkSettings` 增 `XieyiStrokeSettings` 子块 + clamp；`CoreUtils.SetKeyword` 同步；Bloom 仅作用于辉光蒙版。
3. 回归：新增开写意基线 `ink_xieyi_baseline.png`；**关写意时旧墨韵基线逐像素不变（diff==0，C2 守住）**。
4. 性能：墨韵栈（含写意）`ProfilerRecorder` < 3ms（守 §5 预算；写意增量预算 < 0.5ms，复用 S2-R2 口径）。

**要创建/修改的文件**
- `Assets/Shaders/InkFullscreen.shader`（修改：+写意阶段）
- `Assets/Scripts/Rendering/InkRenderFeature.cs`（修改：+XieyiStrokeSettings）
- `Assets/Tests/Baseline/ink_xieyi_baseline.png`（新建，LFS）
- `Assets/Tests/EditMode/XieyiStrokeTests.cs`（新建）

**验收标准**
- ✅ 关写意零回归：`_MJ_XIEYI_STROKE` off 时既有墨韵基线**逐像素不变（diff==0）**（C2 守住，复用 D1 范式）。
- ✅ 开写意正确：施法轨迹一笔晕染、法宝辉光独立、不整体泛白；`ink_xieyi_baseline.png` 基线通过。
- ✅ 单 Pass：Frame Debugger 全屏 Pass 数与 S1 持平（C2 红线，无新增 Pass/Blit）。
- ✅ 参数安全：全参数越界 clamp、无 NaN（EditMode，沿用 E1-S1 ArgumentGuard 模式，CI 可跑）。
- ✅ 性能：墨韵栈 < 3ms（写意增量 < 0.5ms 目标，真机回填）。

**测试证据路径**：`TestResults/editmode-results.xml` + `Assets/Tests/Baseline/ink_xieyi_baseline.png` + Frame Debugger 截图（`production/sprints/evidence/s3/`）。

---

### E1-S5 · 合批与 LOD 守门 — 5 SP

**目标**：SRP Batcher + 静态合批 + GPU Instancing + LOD 规范落地；典型场景 **DrawCall<2000**（S3 里程碑硬指标）。
**技术方案**：**全新**——守 C2/C5、性能预算 §5（DrawCall<2000、LOD 环境 3–4 级/角色 2–3 级）；复用 S2 已验 Toon SRP Batcher 兼容前提（C7）。不引入新渲染 Feature。
**衔接**：生产化守门，依赖 E1-S2 Toon 已就位（C7 SRP Batcher 兼容）；与 E0-S6 Telemetry、E1-S6 InkBudgetGate 形成「性能三件套」。

**拆分子任务**
1. `BatchProbe.cs`：场景加载后统计 DrawCall / SetPass / 合批命中（对接 E0-S6 Telemetry 出口；本 Story 先产出原始计数）。
2. 合批合规编辑器检查 `BatchAuditor.cs`：扫描材质是否兼容 SRP Batcher（无阻断性 `MaterialPropertyBlock`、变量合批友好），输出违规清单。
3. 静态合批标记规范 + GPU Instancing 接入点（植被/碎石等重复件）。
4. LOD 规范 + 样例 `LODGroup` 配置（环境/角色分级）。
5. 验收场景 `BatchReview`：典型箱庭切片，断言 + 截图基线。

**要创建/修改的文件**
- `Assets/Scripts/Rendering/BatchProbe.cs`（新建）、`Assets/Scripts/Editor/BatchAuditor.cs`（新建）
- `Assets/Tests/Scenes/BatchReview.unity`、`Assets/Tests/Baseline/batch_baseline.png`（新建，LFS）
- `Assets/Tests/PlayMode/BatchTests.cs`（新建）

**验收标准**
- ✅ **DrawCall<2000**：`BatchReview` 场景真实硬件下 DrawCall<2000（**S3 里程碑硬指标，真机/LOCAL-CI 门禁，非无头 CI 可证**——DrawCall 计数需 GPU 渲染，同 S2-QA §6 口径：渲染数值走真机）。
- ✅ SRP Batcher 命中：Toon 材质进入 SRP Batch（C7 复核，Frame Debugger 人工 + 截图）。
- ✅ 零每帧审计开销：`BatchProbe` 仅在加载/切场景统计，无每帧成本（Profiler 复核，PlayMode 状态断言无头可跑）。
- ✅ 规范落地：LODGroup 分级配置存在、静态合批标记就位、Instancing 接入点可用（编辑器断言无头可跑）。
- ✅ 不破 C2/C5：无新增全屏 Pass、跨版本单路径。

**测试证据路径**：`TestResults/playmode-results.xml`（结构断言）+ `Assets/Tests/Baseline/batch_baseline.png` + 真机 DrawCall 读数（Telemetry/BatchProbe 落盘，见 M 项 §5）。

---

### E3-S1 · 玩家控制重写 — 8 SP（S3 最大）

**目标**：`PlayerCombatController` 状态机 + 输入接入（取决于 ADR-006 拍板）+ 数据驱动数值；复用灰盒 `PlayerController` 的 i 帧/普攻模式。
**技术方案**：**全新**——重构 `PlayerController`；ADR-006 New Input System **推荐但待主理人确认**（见 §6 P2/P3）；复用 i 帧/普攻模式；**详细战斗状态机设计见 `design/gdd/system-combat-playercontroller.md`（design-strategist 同步产出，本计划不重复）**。
**衔接**：🔴 重构 `Assets/Scripts/Player/PlayerController.cs` → `Assets/Scripts/Combat/PlayerCombatController.cs`；**仅做玩家侧，不引入 `EnemyEntity` 依赖**（`EnemyEntity` 重写属 S4 E3-S2/S3，本 Story 只留碰撞/受击钩子桩，待 S4 接实）。

**拆分子任务**
1. 状态机骨架：Idle / Move / Attack(i 帧普攻) / Dodge(i 帧闪避) / HitStun 状态 + 合法转移表（**数据驱动，转移条件与数值全走 SO/JSON，禁止在 `Update` 里写死数值**）。
2. 输入接入：若 ADR-006 拍板一次性到位 → `PlayerInput` + Input Actions 资产；若暂缓 → 先包输入接口桩，留 legacy→New Input 切换点。
3. 数据驱动数值：`PlayerCombatStats` SO（移速/攻速/无敌帧时长/伤害占位），外部可调，零硬编码。
4. 钩子：普攻命中事件、闪避 i 帧事件、受击事件 → 事件总线占位（待 E3-S2 伤害系统接实）。
5. 测试场景 `CombatControllerReview`：状态转移单测 + 数据驱动数值断言。

**要创建/修改的文件**
- `Assets/Scripts/Combat/PlayerCombatController.cs`（新建，重构自 PlayerController）
- `Assets/Scripts/Combat/PlayerCombatStats.cs` / `.asset`（新建，SO）
- `Assets/Scripts/Input/`（若 ADR-006 到位：PlayerInput + Input Actions 资产；否则接口桩）
- `Assets/Scripts/Player/PlayerController.cs`（完成后标记 `[Obsolete]`，过渡期并存）
- `Assets/Tests/PlayMode/PlayerCombatControllerTests.cs`（新建）

**验收标准**
- ✅ 状态机正确：合法转移通过、非法转移被拒（PlayMode 单测，无头可跑）。
- ✅ 数据驱动：所有战斗数值来自 SO，改 SO 即生效，代码无硬编码魔法数（静态检查/测试断言）。
- ✅ i 帧/普攻复用：普攻模式与闪避 i 帧行为从灰盒 faithfully 复用（行为对拍测试）。
- ✅ 零每帧分配：状态机 `Update` 无 GC 分配（Profiler 复核，呼应 core 零热路径分配）。
- ✅ 输入解耦：战斗逻辑不依赖具体输入后端（legacy 或 New Input 均可驱动，接口桩保证）。

**测试证据路径**：`TestResults/playmode-results.xml` + `Assets/Tests/PlayMode/PlayerCombatControllerTests.cs`。
> ⚠️ **设计前置依赖**：子任务 1/5（状态机落地）须等 `design/gdd/system-combat-playercontroller.md` 到位；设计未到前工程侧先做子任务 2/3/4 的接口桩与 SO 骨架（不依赖设计），并行不空等（见 §2）。

---

### E2-S3 · 排序轴调试可视化 — 2 SP

**目标**：编辑期 gizmo 显示 Y-Z 排序轴，便于美术/策划核对斜 45° 排序。
**技术方案**：**全新**——依赖 E2-S2 `DepthSortBootstrap` 已落地，提取其轴向量画 Gizmo；编辑器仅，放 Editor 文件夹；零运行时成本。
**衔接**：纯编辑器可视化，不进运行时；独立小件。

**拆分子任务**
1. `DepthSortGizmo.cs`（Editor）：场景视图画 `transparencySortMode` 轴 `(0,-0.7071,-0.7071)` 箭头 + 各透明物体排序序位标签。
2. 接线：沿用 S2 `GreyboxBuilder` 自动装配模式或 `[DrawGizmo]`。
3. 编辑器测试：轴向量与 Bootstrap 一致（EditMode 无头）。

**要创建/修改的文件**
- `Assets/Scripts/Editor/DepthSortGizmo.cs`（新建）
- `Assets/Tests/EditMode/DepthSortGizmoTests.cs`（新建）

**验收标准**
- ✅ 轴一致：Gizmo 绘制轴 == `DepthSortBootstrap` 推导轴（EditMode，CI 可跑）。
- ✅ 零运行时成本：Gizmo 仅 Editor 编译，运行时程序集无引用。
- ✅ 可读：场景视图可见轴箭头与序位标签（人工目视）。

**测试证据路径**：`TestResults/editmode-results.xml` + `Assets/Tests/EditMode/DepthSortGizmoTests.cs`。

---

### E1-S6 · 墨韵半分辨率 + 预算守门 — 3 SP

**目标**：墨韵栈 < 2–3ms 守门；半分辨率预案落地（复用 S2-R2 预案）；CI 性能门禁（§5 契约）。
**技术方案**：**全新**——扩展 Ink 半分辨率渲染目标；守 C2 单 Pass + 性能预算 §5；S2-R2 已明确「超线启用半分辨率预案而非拆第二条 Pass」。
**衔接**：扩展 S1 墨韵栈（第三个动 S1 代码的 Story，与 E1-S3/E1-S4 同性质）；半分辨率仅降采样墨韵 RT，主场景全分辨率；对接 E0-S6 Telemetry 读数定阈值。

**拆分子任务**
1. Shader/Feature：墨韵 RT 半分辨率选项（`_MJ_HALF_RES`）+ 上采样合成；主场景分辨率不变。
2. 预算守门：`InkBudgetGate` 在 `ProfilerRecorder` 超阈值时自动降级（半分辨率 + 关非关键阶段），可恢复。
3. 回归：半分辨率开/关两态基线 + 耗时断言（< 3ms）。
4. CI 性能门禁草稿：墨韵栈耗时断言接入 CI（代码逻辑门控走无头；绝对耗时数字留真机回填，同 S2-QA §6）。

**要创建/修改的文件**
- `Assets/Shaders/InkFullscreen.shader`（修改：+半分辨率路径）
- `Assets/Scripts/Rendering/InkRenderFeature.cs`（修改：+半分辨率 RT + InkBudgetGate）
- `Assets/Tests/EditMode/InkBudgetGateTests.cs`（新建）
- `Assets/Tests/Baseline/ink_halfres_baseline.png`（新建，LFS）

**验收标准**
- ✅ 关半分辨率零回归：主路径逐像素不变（C2）。
- ✅ 半分辨率正确：开半分辨率墨韵观感可接受、耗时下降；`ink_halfres_baseline.png` 通过。
- ✅ 预算守门：`ProfilerRecorder` 超 3ms 自动降级且可恢复（逻辑断言 EditMode）。
- ✅ 单 Pass：无新增全屏 Pass（C2）。

**测试证据路径**：`TestResults/editmode-results.xml` + `Assets/Tests/Baseline/ink_halfres_baseline.png`。

---

### E0-S6 · 遥测脚手架 — 3 SP（S2 顺延）

**目标**：`FpsProbe` 演进为 `Telemetry`：DrawCall / 帧时 / 墨韵耗时可采、可上报 CI（帧率冒烟）。
**技术方案**：**全新**——演进 `Assets/Scripts/Core/FpsProbe.cs` → `Assets/Scripts/Services/Telemetry.cs`；复用 FpsProbe 底座 `ProfilerRecorder`；纯 C#、零渲染依赖，EditMode/PlayMode 均可；CI 帧率冒烟接入。
**衔接**：✅ 复用 FpsProbe 底座；本 Story 是 S2 顺延项（roadmap §4 S2 行含 E0-S6，`sprint-02-plan` §0 明确顺延 S3）；与 E1-S5 BatchProbe / E1-S6 InkBudgetGate 形成遥测三件套，**建议早做**以作渲染守门底座。

**拆分子任务**
1. `Telemetry.cs`：统一采集 FPS / 帧时 / DrawCall / SetPass / 墨韵耗时，接口化（供 BatchProbe、InkBudgetGate 复用）。
2. 数据出口：本地日志 + 可选 JSON 落盘（`persistentDataPath`，供 CI 帧率冒烟解析）。
3. CI 帧率冒烟：`ci.yml` 增加解析 Telemetry JSON 步骤（**严守 CI 铁律**：纯 ASCII run 块 / `Start-Process -Wait` / `-runTests` 不加 `-quit`）。
4. `FpsProbe` OnGUI 退役钩子（最终由 E13-S1 HUD 接替，本 Story 仅预留出口，不删 OnGUI，不破 S2 收口）。

**要创建/修改的文件**
- `Assets/Scripts/Services/Telemetry.cs`（新建，演进自 FpsProbe）
- `Assets/Scripts/Core/FpsProbe.cs`（小改：转调 Telemetry 或标记弃用）
- `Assets/Tests/EditMode/TelemetryTests.cs`（新建）
- `.github/workflows/ci.yml`（修改：帧率冒烟解析步骤）

**验收标准**
- ✅ 采集正确：FPS / 帧时 / DrawCall / 墨韵耗时经 `ProfilerRecorder` 采集，数值合理（EditMode 逻辑断言 + 真机回填）。
- ✅ 零渲染依赖：纯 C#，无头可跑（EditMode）。
- ✅ CI 帧率冒烟：`ci.yml` 解析 Telemetry 输出，超阈值失败（严守三条铁律）。
- ✅ 不破 S2 收口：FpsProbe 既有行为不回退。

**测试证据路径**：`TestResults/editmode-results.xml` + CI 帧率冒烟日志。

---

## 2. 依赖排序与并行建议

```
渲染泳道（R）：E1-S4 写意(5) ──► E1-S5 合批(5) ──► E1-S6 半分辨率守门(3)   （均扩展/守墨韵栈与 C2/C5）
编辑器小件：    E2-S3 排序gizmo(2)                                  （依赖 E2-S2 已落地，独立）
战斗泳道（C）：  E3-S1 玩家控制重写(8)   ── 依赖 design-strategist 状态机设计，先出设计再落地
横切底座：      E0-S6 遥测(3)          ── 演进 FpsProbe，早做以作 E1-S5/E1-S6 数据底座
```

- **可完全并行**：渲染泳道 ∥ 战斗泳道 ∥ E2-S3 ∥ E0-S6。E3-S1 与渲染三件事零共享文件；E0-S6 仅演进 FpsProbe，不阻塞任何渲染 Story。
- **渲染泳道建议顺序**：**E1-S4（5）→ E1-S5（5）→ E1-S6（3）**。
  1. E1-S4 先动墨韵栈加写意阶段（与 E1-S3 同性质，复用其 keyword 门控范式，排前面建立基线）；
  2. E1-S5 合批不依赖墨韵内部，但需 Toon 已就位（S2 已就位），可与 E1-S4 并行推进；
  3. E1-S6 半分辨率守门殿后——它要读取 E1-S4/E1-S5 的耗时来定阈值，且是第三个动墨韵代码的 Story，放最后回退风险最低（回退 tag `s1-ink-baseline` 仍可用）。
- **战斗泳道关键建议**：**E3-S1 是 S3 最大（8 SP）且依赖 `design/gdd/system-combat-playercontroller.md` 设计 → 先出设计再落地**。设计未到前，工程侧并行做不依赖设计的子任务（输入接口桩、SO 数值骨架、事件钩子桩），避免空等。设计 GDD 到位后集中落地状态机（子任务 1/5）。
- **E0-S6 早做**：建议 W1 前半完成，使 E1-S5 BatchProbe / E1-S6 InkBudgetGate 直接对接其出口，避免渲染守门 Story 各自造轮子。
- **周节奏建议（2 周冲刺，速率 30，本冲刺 26，留 4 SP 缓冲）**：
  - **W1**：E0-S6（3）+ E2-S3（2）+ E1-S4 子任务 1–2 + E3-S1 接口桩/SO 骨架（设计对接）+ E1-S5 子任务 1–2。
  - **W2**：E1-S4 子任务 3–4 + E1-S5 子任务 3–5（含 `BatchReview` 真机 DrawCall<2000 验收）+ E1-S6（3）+ E3-S1 状态机落地（依赖设计 GDD）+ 回归全绿 + 试玩窗口。
  - **缓冲**：约 4 SP（或吸收 S2 遗留 A11 补测 / 像素级基线采集协助）。
- **对外依赖（非阻塞）**：design-strategist 的 `system-combat-playercontroller.md`——状态机落地等它；美术侧 Toon 视觉参数（S2 已对齐骨架，本冲刺不阻塞）。

## 3. Story Point 复核

| Story | 路线图原估 | 复核后 | 说明 |
|-------|-----------|--------|------|
| E1-S4 | 5 | **5** | 扩展墨韵栈 + keyword 门控，复用 S1/S2 范式；成本在基线 + 性能断言 |
| E1-S5 | 5 | **5** | 合批审计 + 规范 + LOD；成本在 `BatchReview` 真机验收；DrawCall<2000 需真机门禁 |
| E3-S1 | 8 | **8** | 最大；成本在状态机 + 数据驱动 + 复用 + 测试；**若 ADR-006 一次性到位含 New Input 迁移则 8 成立；若暂缓则本 Story 缩为接口桩+状态机（≈6），New Input 留后续（见 §6 P2）** |
| E2-S3 | 2 | **2** | 编辑器 gizmo，独立小件 |
| E1-S6 | 3 | **3** | 复用 S2-R2 预案 + 半分辨率 RT |
| E0-S6 | 3（S2 顺延） | **3** | 演进 FpsProbe，复用底座 |
| **合计** | 23（+3 顺延=26） | **26** | 速率 30 → 留 **4 SP** 缓冲（或吸收 S2 遗留 A11 补测） |

## 4. 风险评估 + 控制规则新增

| # | 风险 | 概率/影响 | 缓解 |
|---|------|----------|------|
| **S3-R1** | 墨韵栈第三/四次修改（E1-S4/E1-S6 动 S1 已验证代码）引入回归，C2 单 Pass 被破坏 | 中/高 | 排最后做；关阶段逐像素不变硬验收（diff==0）；回退 tag `s1-ink-baseline` 仍可用；改动前打 tag |
| **S3-R2** | DrawCall<2000 里程碑在 950M 上难达（合批/LOD 不足或场景过重） | 中/高 | E1-S5 早做 BatchProbe 审计；半分辨率(E1-S6)作墨韵侧兜底；超线按预算门禁降级而非堆 Pass |
| **S3-R3** | E3-S1 依赖 design-strategist 状态机设计未到，工程侧阻塞 | 中/高 | 先出设计再落地；设计未到前做接口桩/SO 骨架（不依赖设计的子任务）；并行不空等 |
| **S3-R4** | New Input 迁移(ADR-006) 范围不清：一次性到位 vs 接口桩，影响 E3-S1 验收口径 | 中/中 | §6 拍板；若暂缓，状态机先 legacy 驱动，留切换点 |
| **S3-R5** | 状态机在 `Update` 写死数值，后续调参难、回归脆 | 高/中 | 控制规则 6：数值全数据驱动（SO/JSON），Update 零硬编码；静态检查/测试断言 |
| **S3-R6** | 8GB 内存 CI 仍紧（S2-R1 延续）；E0-S6 加 CI 帧率冒烟步骤增 Unity 冷启 | 中/中 | 沿用 S2 两 job 串行；页面文件 16–24GB；冒烟步骤不带 `-nographics` |
| **S3-R7** | 像素级基线仍 pending（S2 遗留）：E1-S4 写意新增基线需制作人本机采集 | 中/中 | 沿用 S2 §4 采集纪律；连续 3 次 diff==0 入库；走 LFS；E1-S4 基线列 S3 采集清单 |
| **S3-R8** | 半分辨率上采样伪影/墨线锯齿 | 低/中 | 半分辨率仅降墨韵 RT，主场景全分辨率；基比对拍；伪影超阈则回退全分辨率 |

**控制规则新增（复用 S2 §4 五条 + 补战斗/渲染守门规则）**：

> S2 既有（复用，不重述全文）：① 多面片组合体必挂 `SortingGroup`；② 禁止为排序把不透明材质改 Transparent；③ Toon 材质禁出现描边参数；④ 全屏效果只允许并入墨韵 Pass（新 Feature 须 ADR）；⑤ 存档字段变更必须 `saveVersion+1` 并补 upgrader + 测试。

S3 新增：

6. **玩家状态机禁止在 `Update` 里写死数值，全数据驱动（SO/JSON）**（S3-R5 红线）。
7. 战斗状态转移表须由单测覆盖（合法转移通过、非法转移被拒），禁止未测试的转移分支。
8. 墨韵栈任何新阶段必须 `keyword` 门控，关时旧基线逐像素不变（diff==0）；复用 ADR-010 范式。
9. 扩展墨韵栈（写意/半分辨率）不得新增全屏 Pass（C2）；超预算走半分辨率降级而非堆 Pass。
10. 遥测采集纯 C# 零渲染依赖，可无头跑；CI 帧率冒烟严守三条铁律（run 纯 ASCII / 启 Unity 必 `Start-Process -Wait` / `-runTests` 绝不加 `-quit`）。
11. **E3-S1 仅做玩家侧，不得引入 `EnemyEntity` 依赖**（`EnemyEntity` 重写属 S4 E3-S2/S3）。

---

## 5. S3 末制作人试玩窗口（大白话）

**触发条件**：六 Story 验收全绿 + CI 绿（含 S2 既有 EditMode 57 / PlayMode 4 不回退）。

**能试玩/能看什么（大白话）**：
- 玩家能**走动 + 普攻 + 闪避**（战斗骨架搭起来了；数值是占位的，手感先粗调，后面数据驱动慢慢磨）。
- 施法时有**一笔写意的墨痕**观感雏形（E1-S4）。
- 场景的 **DrawCall 守在 2000 以下**（性能命门，E1-S5 + E1-S6 守门）。
- 墨韵栈开了**半分辨率守门**，950M 上更稳（E1-S6）。
- 编辑器里能看到**排序轴箭头**给美术/策划核对（E2-S3）。
- 遥测读数能看 FPS / DrawCall / 墨韵耗时（E0-S6）。

**制作人要报回什么（写进试玩反馈即可）**：
1. **H4 打击感初评占位**：本冲刺只搭骨架，真正的打击感组合（顿帧/墨溅/震屏，E3-S5）在 S5，所以这里只是「骨架手感第一印象」占位，**非终评**。
2. **战斗手感**：移动 / 普攻 / 闪避的「跟手程度」第一印象——数值后面数据驱动调，先听你的主观感觉。
3. **帧率**：950M 上试玩时 FPS 是否仍 **≥58**（开/关写意、半分辨率各看一次）；**DrawCall 是否 < 2000** 看遥测读数（这两条是 S3 里程碑硬指标）。

---

## 6. 待主理人拍板项

| # | 事项 | 建议 |
|---|------|------|
| **P1** | **E0-S6 遥测（3 SP）是否正式纳入 S3** | 建议纳入（已顺延，且是 E1-S5/E1-S6 守门的数据底座）；纳入后 S3=26 SP，缓冲 4 |
| **P2** | **New Input System 迁移(ADR-006) 在 E3-S1 内是否一次性到位** | 若到位 → E3-S1 含 `PlayerInput` + Input Actions（8 SP 成立）；若暂缓 → 先接口桩、状态机 legacy 驱动，New Input 留后续（E3-S1 ≈ 6 SP，余 2 顺延）；**建议一次性到位以消技术债** |
| **P3** | ADR-006 状态仍为「推荐待确认」→ 是否在本冲刺前正式批准 | 批准则 P2 可一次性到位；否则按接口桩走 |
| **P4** | **路线图 §4 口径漂移**：S2 行仍列 E0-S6（27 SP）、S3 行仅 23 SP，与「E0-S6 顺延 S3、S2 实收 24」现实不符 | 建议更新 roadmap §4：S2=24（实收口 4 Story）、S3=26（含 E0-S6）；详见文末冲突说明 |
| **P5** | E1-S4/E1-S6 新增像素基线采集（S2 像素基线仍 pending）是否需制作人本机协助 | 沿用 S2 §4 采集纪律（连续 3 次 diff==0 入库、走 LFS）；列 S3 采集清单，需制作人本机带 GPU 跑 |

---

### 附：与 roadmap 口径冲突说明（供主理人回写 roadmap）

- **冲突点**：`production/roadmap/sprint-plan.md` §4 的 **S2 行**列 `E0-S5(8)·E0-S6(3)·E1-S2(8)·E2-S2(5)·E1-S3(3)=27 SP`，但 `sprint-02-plan.md` §0 已明确把 E0-S6 **顺延进 S3**，S2 实际收口为 **24 SP（4 Story）**；而 **S3 行**列 `E1-S4(5)·E1-S5(5)·E3-S1(8)·E2-S3(2)·E1-S6(3)=23 SP`，未含已顺延的 E0-S6。
- **本计划立场**：S3 按 **26 SP（含 E0-S6）** 规划；建议 roadmap §4 同步改为 S2=24、S3=26，使排期表与现实一致（不影响 S3 里程碑「DrawCall<2000 守门 + 玩家战斗状态机就位」的成立）。

---

*产出清单：本文件（规划先行，批准后进入实现，本任务不写代码、不 git commit）。实施代码与可能新增的 ADR（如 E1-S6 半分辨率确需独立 ADR）不在此任务范围。*
