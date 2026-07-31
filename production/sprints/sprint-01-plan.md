# Sprint 1 实现计划 — 《秘境·凡尘》生产工程地基

> 文档状态：ENG-001 派生 / Sprint 1 实现计划（待主理人 + 制作人按本计划启动）
> 关联：`production/roadmap/sprint-plan.md` §3(E0)/§4(S1)、`../../docs/architecture/production-architecture.md`（ADR-001~006 内嵌）、`production/ci/ci-workflow-template.yml`、`../../docs/onboarding/task1-unity-setup.md`、`../../docs/onboarding/task2-prototype-run.md`
> 受众：工程侧（程基岩 + AI 代码生成）+ 制作人（仅看 §0 卡点总览与 §3 试玩窗口）
> 范围：S1 地基 6 个 Story，合计 **27 SP**（E0-S1/S2/S3/S4 + E1-S1 + E2-S1）
> 严守约束：C1 引擎锁 2022.3（禁 Unity 6）、C2 墨韵单 Pass、C5 跨版本单路径（无版本宏/无 RTHandle/RenderGraph）、R1 消除混合态。

---

## 0. 前置依赖与制作人卡点总览

S1 有 3 个**必须由制作人先完成**的环境卡点（详见 `sprint-01-producer-checklist.md`）。工程侧在卡点未清除前**无法验收**，但可以在制作人并行推进环境搭建的同时，先做不依赖本地 Unity 的准备工作（仓库结构草稿、CI 模板裁剪、`.gitattributes` 起草）。

| # | 卡点（⚠️ 需制作人先完成环境搭建） | 阻塞的 Story | 不完成会怎样 |
|---|-------------------------------|-------------|-------------|
| **B1** | 制作人本机安装 **Unity 2022.3.62 LTS**（匹配 URP v14.x） | E0-S1、E0-S3、E1-S1、E2-S1 | 无法 `clean checkout + 编译通过`；混合态（2022.3 编辑器 + URP v17）风险复发；InkRenderFeature 行为不一致 |
| **B2** | 创建 **GitHub 私有仓库** + 安装 **Git + Git LFS** | E0-S2、E0-S3 | 无仓库则 VCS/LFS 初始化与 CI 触发都无从接线；LFS 配额未定则大资产 clone 不完整 |
| **B3** | 在制作人本机配置 **自托管 GameCI runner**（注册 + Unity 许可证激活） | E0-S3 | CI 流水线无法跑起来，`push main` 无构建、无墨韵回归、无帧率门禁 |

> **进度建议**：B1/B2/B3 由制作人按 `sprint-01-producer-checklist.md` 推进；工程侧可并行起草仓库结构与 CI 模板（见各 Story「可在卡点前并行」项）。**B1/B2/B3 全绿 = S1 工程工作可启动验收**。

---

## 1. 逐 Story 实现计划

> 约定：`<REPO_ROOT>` = 制作人 clone 的 GitHub 私有仓库本地根目录（即 Unity 工程目录，例如 `D:\WBzone\Game\mijing-fanchen\`）。`<WORKSPACE>` = `D:\WBzone\Game`（设计/文档工作区，本任务不改动其游戏代码）。

---

### E0-S1 · 版本对齐与项目结构 — 5 SP

**目标**：消除「2022.3 编辑器 + URP v17」混合态（R1），干净 checkout 在 2022.3 匹配 URP 版本编译通过；`InkRenderFeature` 无报错；目录规范落地。
**衔接**：直接处理混合态 R1；墨韵栈（ADR-002）作为地基进生产，仅加固不重写。
**复用**：`prototype/Assets/Scripts/Rendering/InkRenderFeature.cs`、`Shaders/InkFullscreen.shader`、`Camera/CameraRig.cs`、`Core/FpsProbe.cs`、`Core/GreyboxBuilder.cs`、`Editor/GreyboxMenu.cs`、`Editor/InkMaterialCreator.cs`、`Player/PlayerController.cs`、`Enemy/DummyEnemy.cs` 移入生产仓库。

**具体技术步骤**
1. 用 **Unity 2022.3.62 LTS** 新建 3D(URP) 工程于 `<REPO_ROOT>`（禁用 Unity 6，C1）。
2. 打开 `Packages/manifest.json`，将 `com.unity.render-pipelines.universal` 与 `com.unity.render-pipelines.core` 钉死为 **14.x**（读 2022.3.62 模板解析出的确切 14.x 版本回填，拒绝 Package Manager 的「升级到 v17」提示）。
3. 落目录规范（见下「要创建/修改的文件」），把 `prototype/Assets` 下 9 个原型文件按模块路径移入 `Assets/Scripts/<Module>/` 与 `Assets/Shaders/`。
4. 在 URP Renderer 资产挂 `InkRenderFeature`，指派 `InkMaterial`（复用 `InkMaterialCreator` 菜单生成）；确认 `ConfigureInput(Depth)` + `cameraColorTarget` 单路径编译通过（C5）。
5. 写 `README.md`（一句话说明 + 如何打开）；写 `.gitignore`（排除 `Library/`、`Temp/`、`obj/`、`Logs/`）。
6. **可在卡点前并行**：工程侧先起草 `manifest.json` URP 14.x 片段、`.gitignore`、目录骨架说明，等 B1 就绪后由制作人在本地打开验证。

**要创建/修改的文件（路径）**
- `Packages/manifest.json`（修改：URP 钉 14.x）— `<REPO_ROOT>/Packages/manifest.json`
- `Assets/Scripts/Rendering/InkRenderFeature.cs`（移入，来自 prototype）
- `Assets/Scripts/Shaders/InkFullscreen.shader`（移入）— 或保留 `Assets/Shaders/`
- `Assets/Scripts/Camera/CameraRig.cs`、`Assets/Scripts/Core/{FpsProbe,GreyboxBuilder}.cs`
- `Assets/Scripts/Editor/{GreyboxMenu,InkMaterialCreator}.cs`
- `Assets/Scripts/Player/PlayerController.cs`、`Assets/Scripts/Enemy/DummyEnemy.cs`
- `Assets/Settings/`（URP Asset + Renderer + Volume Profile 固化）
- `README.md`、`.gitignore`（新建）

**验证命令或断言**
- 编辑器打开工程 → Console **0 编译错误**；`InkRenderFeature` Inspector 无 obsolete 升级错误（C5 单路径生效）。
- 批处理编译校验：`"<Unity>/Editor/Unity.exe" -batchmode -projectPath <REPO_ROOT> -quit -logFile -`（退出码 0 = 通过）。
- 运行灰盒：点 `Greybox → Rebuild Scene` + ▶ 后呈现水墨质感（对齐 `task2-prototype-run.md` 验收）。

**完成判据**
- ✅ `git clone` 后首次 Unity 打开 **0 报错**；✅ `InkRenderFeature` 无编译/运行错误；✅ 目录规范落地且 9 个原型按模块归位；✅ `manifest.json` URP = 14.x 已钉死。

> ⚠️ **制作人卡点**：依赖 **B1（装好 Unity 2022.3.62 LTS）**。制作人未装好前，本 Story 只能在 `prototype/` 侧做静态准备，无法在 `<REPO_ROOT>` 编译验收。

---

### E0-S2 · VCS + LFS 初始化 — 3 SP

**目标**：Git 仓库 + LFS track（贴图/模型/音频）+ trunk-based 分支；clone 资源完整。
**衔接**：全新；为 E0-S3（CI）与全栈资产托管打底（ADR-004）。

**具体技术步骤**
1. 制作人按 B2 创建 **GitHub 私有仓库**（空仓库，默认 `main` 分支）。
2. 工程侧在 `<REPO_ROOT>` 初始化 Git：`git init` → 接远程 `main` → 首提交（含 E0-S1 的目录结构与 `manifest.json`）。
3. 写 `.gitattributes`，把大资产纳入 LFS：`*.png *.jpg *.jpeg *.tga *.psd *.tiff`、`*.fbx *.obj *.blend *.mesh`、`*.wav *.mp3 *.ogg *.aiff`。
4. 确立 **trunk-based**：`main` 为集成分支；feature 分支短生命周期（≤2 天），每日向 `main` 合流；发布用 `tag` + `release/*` 分支。
5. 验证 LFS：`git lfs install` → `git lfs track` 列出规则；提交一个贴图样本，确认仓库里是 LFS 指针而非二进制本体。
6. **可在卡点前并行**：工程侧先起草 `.gitattributes` 与分支策略文档，等 B2 仓库就绪后由制作人接远程首推。

**要创建/修改的文件（路径）**
- `.gitattributes`（新建，LFS track 规则）— `<REPO_ROOT>/.gitattributes`
- `.github/` 分支保护说明（写入 README 或 `docs/`）
- 首提交内容：E0-S1 全部文件 + `manifest.json`

**验证命令或断言**
- `git lfs ls-files` 能列出已跟踪的大资产；`git lfs pull` 后本地贴图/模型/音频为**真实二进制**（非 130B 指针）。
- `git branch -a` 仅见 `main` + 临时 feature；PR 合入 `main` 后 `main` 可编译（接 E0-S3）。

**完成判据**
- ✅ 私有仓库存在且 `main` 可读；✅ `.gitattributes` LFS 三类齐全；✅ trunk-based 分支策略落地；✅ `git clone` + `git lfs pull` 后资源完整（LFS 指针已替换）。

> ⚠️ **制作人卡点**：依赖 **B2（GitHub 私有库 + Git + Git LFS 安装）**。制作人未建库前，本 Story 仅能起草 `.gitattributes`，无法首推验证。

---

### E0-S3 · CI 流水线 bootstrap — 8 SP

**目标**：push/PR `main` 触发 GameCI 构建；**墨韵回归 + 帧率冒烟**卡门禁；产出可执行。
**衔接**：全新（ADR-004）；自托管 runner 零云成本（Q1 已拍板）。
**复用**：基于 `production/ci/ci-workflow-template.yml` 裁剪。

**具体技术步骤**
1. 把 `production/ci/ci-workflow-template.yml` 复制为 `<REPO_ROOT>/.github/workflows/ci.yml`，并按 3 处注释改造：
   - ① `unityVersion` 钉 `2022.3.62f1`（与 B1 编辑器同补丁族；中国区 `f3c1` API 等价）。
   - ② 接墨韵回归 + 帧率冒烟脚本（来自 E1-S1 的回归套件路径）。
   - ③ 确认 `manifest.json` URP=14.x（已在 E0-S1 钉死）。
2. `runs-on: self-hosted`（自托管，避免云端拉到非 2022.3）。
3. 构建：`game-ci/unity-builder@v4`，`targetPlatform: StandaloneWindows64`，`buildMethod: UnityBuilderAction.BuildPlayer.Default`，产出到 `build/`。
4. 门禁（顺序）：编译 → 墨韵回归（E1-S1 套件：截图/耗时断言）→ 帧率冒烟（FPS ≥ 58，H2）→ 上传 `build/` 为 artifact。
5. 配置仓库 Secrets：`UNITY_LICENSE`（制作人本机 Unity 许可证激活导出，CI 注入，不进仓库，呼应 ADR-005 密钥管理）。
6. **可在卡点前并行**：工程侧先裁剪 `ci.yml` 模板、写门禁占位与 README 说明；等 B3（runner 注册 + 许可证）后由制作人触发首次运行。

**要创建/修改的文件（路径）**
- `.github/workflows/ci.yml`（新建，源自 `production/ci/ci-workflow-template.yml`）
- 仓库 Secrets：`UNITY_LICENSE`（制作人配置，不入文件）
- `README.md` 追加「CI 状态 / 如何下载试玩构建」一节

**验证命令或断言**
- 触发：`git push origin main`（或向 `main` 开 PR）→ GitHub Actions 自动运行。
- 断言：`gh run list` 显示 `ci` 成功；Artifacts 有 `game-build`；日志含「Ink 回归 PASS」「FPS ≥ 58 (H2)」。
- 失败信号：URP 被升到 17 / 版本不匹配 → 构建红，CI 卡住（即 R1 防护生效）。

**完成判据**
- ✅ `push main` 自动触发 GameCI；✅ 墨韵回归 + 帧率冒烟为**强制门禁**（任一不过则红）；✅ 产出 `build/` 可执行且可下载试玩。

> ⚠️ **制作人卡点**：依赖 **B2（仓库）+ B3（自托管 runner 注册 + `UNITY_LICENSE` 注入）**。两者未就绪，CI 无法触发，门禁无法验证。

---

### E0-S4 · 输入系统迁移基底 — 5 SP

**目标**：New Input System 接入；`Input Actions` 资产骨架；灰盒临时映射可玩。
**衔接**：重构 `PlayerController` 前置（ADR-006）；为 E3-S1 状态机铺路。
**复用**：灰盒 `PlayerController.cs`（WASD/闪避 i 帧/普攻模式）保留，仅换输入源。

**具体技术步骤**
1. Player Settings → Active Input Handling 设为 **Input System Package (New)** 或 **Both**（过渡期用 Both，避免灰盒不可用）。
2. 新建 `Assets/Input/GameInput.inputactions`，建 Action Map `Player`：
   - `Move`（Vector2，WASD）、`Attack`（button，鼠标左键）、`Dodge`（button，空格）、`Interact`（button，E）。
3. 写桥接脚本 `Assets/Scripts/Input/InputBridge.cs`：读 `PlayerInput` 事件 → 喂给现有 `PlayerController`（不改战斗逻辑，仅换输入源），保留临时灰盒映射可玩。
4. 验证：灰盒场景 ▶ 后 WASD 移动、空格翻滚、左键攻击仍生效（与 `task2-prototype-run.md` 手感一致）。
5. **可在卡点前并行**：`.inputactions` 资产骨架与桥接脚本可在 E0-S1 编译通过后即写（不依赖 B2/B3，仅需 B1 能编译）。

**要创建/修改的文件（路径）**
- `Assets/Input/GameInput.inputactions`（新建，Action 资产骨架）
- `Assets/Scripts/Input/InputBridge.cs`（新建，桥接层）
- `ProjectSettings/ProjectSettings.asset`（修改：Active Input Handling）
- `Assets/Scripts/Player/PlayerController.cs`（小幅改：输入源从 legacy 切到桥接）

**验证命令或断言**
- Player Settings 显示 New Input System 已启用；`GameInput.inputactions` 在 Inspector 可编辑。
- 运行时：绑定 `Move/Attack/Dodge/Interact` 均触发（输入调试面板 `Window → Analysis → Input Debugger` 可见事件）。

**完成判据**
- ✅ New Input System 接入；✅ `Input Actions` 资产骨架含 Move/Attack/Dodge/Interact；✅ 灰盒临时映射**可玩**（WASD/空格/左键生效），手感与灰盒期一致。

> ⚠️ **制作人卡点**：依赖 **B1（Unity 2022.3.62 能打开工程）** 做编译验收；不依赖 B2/B3（本地即可验证）。B1 未就绪则无法编译验收，但资产骨架可先起草。

---

### E1-S1 · 墨韵回归 + 参数校验 — 3 SP

**目标**：自动化回归（截图/耗时断言）；参数越界保护（直接进生产加固）。
**衔接**：直接进生产 `InkRenderFeature`/`InkFullscreen.shader`（加固）；接 E0-S3 门禁。
**复用**：`InkRenderFeature.cs` 参数（`lineThickness/lineStrength/paperStrength/feibaiThreshold/inkStainStrength`）。

**具体技术步骤**
1. 新建回归套件 `Assets/Tests/EditMode/InkRegression.cs` + `Assets/Tests/Runtime/InkRuntimeRegression.cs`（Unity Test Framework）：
   - **截图断言**：固定场景渲染墨韵 → 与基线 `Tests/Baseline/ink_baseline.png` 比对（像素差异阈值内 = 通过）；基线由首跑人工确认后入库（LFS）。
   - **耗时断言**：用 `ProfilerRecorder` 采 Ink Pass 耗时，断言 `< 3ms`（性能契约 §5）。
   - **参数越界保护**：把 `lineThickness` 设 999 / 0 / -5，`paperStrength` 设 2 等越界值 → 断言着色器输出**无 NaN/Inf、无异常、被 clamp 到合法区间**（在 `InkRenderFeature` 设值处做 `Mathf.Clamp`）。
2. 把回归套件接入 E0-S3 的 CI 门禁（截图 + 耗时断言作为强制步骤）。
3. 工程加固：在 `InkRenderFeature` 设参处加 `Mathf.Clamp`（如 `lineThickness` ∈ [0.5,5]），并记录越界日志。

**要创建/修改的文件（路径）**
- `Assets/Tests/EditMode/InkRegression.cs`、`Assets/Tests/Runtime/InkRuntimeRegression.cs`（新建）
- `Assets/Tests/Baseline/ink_baseline.png`（LFS，首跑入库）
- `Assets/Scripts/Rendering/InkRenderFeature.cs`（修改：参数 clamp 加固）

**验证命令或断言**
- 本地：`Unity -runTests -projectPath <REPO_ROOT> -testPlatform EditMode -testResults results.xml` → Ink 套件全绿。
- CI：E0-S3 门禁步骤调用同一套件，失败则构建红。
- 越界用例：单测 `ArgumentGuard` 断言 clamp 后值 ∈ 合法区间。

**完成判据**
- ✅ 自动化回归套件存在且**接 CI 强制门禁**；✅ 截图 + 耗时（<3ms）断言通过；✅ 参数越界被 clamp，无 NaN/异常/崩溃。

> ⚠️ **制作人卡点**：依赖 **B1（能编译运行）+ E0-S3（CI 门禁接线）**。本地可先跑单测；CI 强制门禁需 B3 就绪。

---

### E2-S1 · 多目标 framing — 3 SP

**目标**：2–3 连通区切换关键信息不出框/不遮挡（H5 Pass）；直接进生产扩展 `CameraRig`。
**衔接**：直接进生产 `CameraRig.cs`（扩展）；呼应 C3/C4（固定斜45° + Y-Z 排序轴）。
**复用**：`prototype/Assets/Scripts/Camera/CameraRig.cs`（低 FOV 斜45° 锁定，禁旋转）。

**具体技术步骤**
1. 扩展 `CameraRig`：增加「目标组跟随 / framing」模式——维护 2–3 个连通区关键目标（Transform 列表），相机自动取景使关键目标在视口内、不互相遮挡。
2. 建 H5 测试场景 `Assets/Tests/Scenes/H5Framing.unity`：2–3 连通区 + 代表关键目标（敌人/出口/Boss）。
3. 写断言：每帧把关键目标世界坐标投影到屏幕 UV，断言 **∈ [margin, 1-margin]**（不出框）；对关键目标做视线 raycast，断言 **无遮挡**（或遮挡时长 < 阈值）。
4. 验证 H5 Pass：切换连通区时关键信息持续可见、不遮挡。

**要创建/修改的文件（路径）**
- `Assets/Scripts/Camera/CameraRig.cs`（修改：加 framing/目标组跟随）
- `Assets/Tests/Scenes/H5Framing.unity`（新建测试场景）
- `Assets/Tests/Runtime/CameraFramingTest.cs`（新建 H5 断言）

**验证命令或断言**
- 运行时断言：关键目标屏幕 UV 在视口内比例 ≥ 100%−margin；遮挡 raycast 命中自身比例达标。
- 人工复核：制作人试玩窗口（§3）肉眼确认 H5。

**完成判据**
- ✅ `CameraRig` 支持多目标 framing；✅ H5 测试场景断言通过（关键信息不出框/不遮挡）；✅ 2–3 连通区切换 H5 Pass。

> ⚠️ **制作人卡点**：依赖 **B1（能编译运行）+ E0-S1（CameraRig 已进生产）**。本地可跑断言；无需 B2/B3。

---

## 2. S1 验收门 ↔ sprint-plan.md 验收标准 对照表

| Story | S1 验收门（本计划定义的可验证门槛） | sprint-plan.md §3 验收标准 | 验证手段 |
|-------|-----------------------------------|---------------------------|---------|
| **E0-S1** | G1：`manifest.json` URP=14.x；Unity 2022.3.62 打开 0 编译错误；`InkRenderFeature` 无报错；目录规范落地 | 干净 checkout 在 **2022.3 匹配 URP 版本**编译通过；`InkRenderFeature` 无报错；目录规范落地 | 编辑器 Console + 批处理编译退出码 0 |
| **E0-S2** | G2：GitHub 私有库 + `main`；`.gitattributes` LFS 三类；trunk-based；`git lfs pull` 资源完整 | Git 仓库 + LFS track（贴图/模型/音频）+ trunk-based 分支；clone 资源完整 | `git lfs ls-files` + `git lfs pull` 比对 |
| **E0-S3** | G3：`push main` 触发 GameCI；门禁含墨韵回归 + FPS≥58；产出 `build/` 可执行 | push main 触发 GameCI 构建；**墨韵回归 + 帧率冒烟**卡门禁；产出可执行 | `gh run` 状态 + Artifact `game-build` |
| **E0-S4** | G4：New Input 启用；`GameInput.inputactions` 含 Move/Attack/Dodge/Interact；灰盒临时映射可玩 | New Input System 接入；`Input Actions` 资产骨架；灰盒临时映射可玩 | Player Settings + Input Debugger + 手感复核 |
| **E1-S1** | G5：回归套件接 CI 强制门禁；截图 + 耗时(<3ms) 断言通过；参数越界 clamp 无 NaN | 自动化回归（截图/耗时断言）；参数越界保护 | `Unity -runTests` + CI 门禁步骤 |
| **E2-S1** | G6：`CameraRig` 多目标 framing；H5 场景断言通过（不出框/不遮挡） | 2–3 连通区切换关键信息不出框/不遮挡（H5 Pass） | 屏幕 UV 投影 + 遮挡 raycast 断言 |

**S1 总出门判据**：G1~G6 全绿 + 制作人试玩窗口（§3）可触发 = Sprint 1 完成。

---

## 3. S1 末制作人试玩窗口说明

**触发条件**：G1~G3 通过（CI 可执行 + 墨韵栈回归绿）。
**可试玩内容**：**现有灰盒 + 加固后的墨韵栈**（不写新游戏业务代码，仅地基）。
- 斜 45° 固定视角的水墨/宣纸风灰盒场景（H2/H3 观感）。
- 黄色玩家（WASD 移动、空格翻滚、左键攻击）、绿/青/红敌人（地面/浮空/Boss）。
- 左上角 FPS / Draw Calls / Triangles（FpsProbe 底座）。
- 多目标 framing（E2-S1）初步生效：切换连通区时关键信息不出框。

**制作人要做的事（对齐 `task2-prototype-run.md` 的 H1/H2）**
1. 从 CI Artifact 下载 `game-build` 可执行，双击运行（或本地 Unity ▶ 运行）。
2. 报回 **H1 可读性**（斜45° 下能否分清地面/浮空敌人）+ **H2 帧率**（FPS 是否稳在 60 左右）。
3. 顺便感受加固后墨韵参数有无异常（越界保护已生效，理论上更稳）。

**反馈回路**：试玩结论回传工程侧 → 决定是否进 S2（渲染起步：Toon/E1-S2、Y-Z 排序/E2-S2、存档/E0-S5）。

---

## 4. 风险与待拍板

| # | 项 | 说明 | 建议 |
|---|----|------|------|
| R1 | 混合态残留 | 若 B1 装成非 2022.3 或 URP 被升 17，CI 红 + 墨韵漂移 | CI 门禁卡死；`manifest.json` 钉 14.x（E0-S1） |
| R10 | 构建环境漂移 | runner 与制作人编辑器补丁不一致 | 两者钉同一 2022.3.62 补丁族（f1/f3c1 API 等价） |
| **待拍板** | **Unity 确切补丁 + URP 14.x 确切版本** | 建议钉 **2022.3.62**（CI 模板 `2022.3.62f1`，制作人本机 `2022.3.62f3c1` 等价）；URP 钉 14.x（读 2022.3.62 模板解析值回填） | 制作人装好后由工程侧读 `manifest.json` 确认并钉死 |
| **待拍板** | **runner 机器规格假设** | 假设 = 制作人本机 Windows 10/11 64 位、16GB+ RAM、50GB+ 空闲（Unity+Library）、带可跑 1080p 游戏的 GPU；装 Unity 2022.3.62 + Windows Build Support(IL2CPP) + 有效 Unity 许可证 | 请制作人/主理人确认机器满足，否则需专用构建机 |
| **文档缺口** | ADR/CLAUDE/engine-reference/control-manifest | `../../docs/architecture/adr-*.md`、`CLAUDE.md`、`../../docs/engine-reference/<engine>/VERSION.md`、`../../docs/architecture/control-manifest.md` 当前**不存在**（ADR-001~006 内嵌于 `production-architecture.md`） | 建议 S1 期间补：`control-manifest.md`（一页可执行规则）、`CLAUDE.md`（技术偏好）、独立 `adr-*.md`、engine-reference VERSION.md |

---

*回传主理人：文件已落地 `production/sprints/sprint-01-plan.md` 与 `production/sprints/sprint-01-producer-checklist.md`；S1 范围与 Top 3 卡点见 SendMessage。*
