# 生产架构文档 — 《秘境·凡尘》Phase 5 全量生产

> 文档状态：ENG-001 交付物 1 / 架构蓝图（待主理人拍板后进入实现）
> 关联文档：`tech-assessment-2.5d-combat.md`(ADR-001/002/003)、`greybox-plan.md`(S1–S9)、`design/game-concept.md`(8 系统)、`production/roadmap/sprint-plan.md`
> 受众：工程侧（程基岩 + AI 代码生成）+ 制作人（环境搭建/试玩反馈，非程序）

---

## 0. 来源与范围

本文档把 **Phase 2 已验证的灰盒方案**（墨韵单 Pass、2.5D 固定斜 45°、互锁箱庭可读性、帧率探针）作为**不可推翻的底座**，向上延展为 Phase 5 全量生产的工程蓝图。

- **输入端**：8 大系统（概念草案）+ 灰盒 S1–S9（已交付代码）+ 现有 9 个原型文件。
- **输出端**：模块划分、依赖与数据流、关键技术决策（ADR 补充）、性能预算、风险登记、原型代码进生产评估。
- **不推翻**：墨韵栈（单条 Ink Render Feature 全屏 Pass）、2.5D 固定相机、Y-Z 深度排序轴、已验证的可玩/帧率结论。
- **本阶段范围**：垂直切片 + v1 首章（1 互锁箱庭 → 3–5 箱庭 + 洞府 hub 闭环 + 3 段境界链）。v2（宗门/仙界篇）架构预留接口，不在 ENG-001 实现清单内。

---

## 1. 不可推翻的工程约束（Non-Negotiables）

| # | 约束 | 来源 | 违背后果 |
|---|------|------|----------|
| C1 | **引擎锁 Unity 2022.3 LTS + URP，严禁升级 Unity 6** | ADR-001 | Unity 6 的 RenderGraph 会摧毁现有 Shader/Feature（ADR-002 注释明确标注） |
| C2 | **墨韵 = 单条自定义 Ink Render Feature 全屏 Pass** | ADR-002 | 多 Pass 叠加吃掉 PC 帧率，中等体量不可承受 |
| C3 | **2.5D 固定斜 45° 微俯、禁自由旋转** | 灰盒 S1、技术评估 | 自由旋转破坏深度排序与相机管理前提 |
| C4 | **深度排序用自定义 Y-Z 轴（非纯 Z）** | 技术评估风险表 | 斜 45° 下纯 Z 排序会错乱，角色穿插 |
| C5 | **代码须跨版本安全（单路径、不写版本宏、不引入 RTHandle/RenderGraph）** | `InkRenderFeature.cs` 头部注释 | 当前为「2022.3 编辑器 + 被升到 v17 的 URP 包」混合态，版本宏会判断错目标 |
| C6 | **不可再生经济 + 死亡掉落是反通胀基石**（P1 稀缺即命运） | 概念草案 §6.1 | 掉落 ≥ 消耗会稀释核心幻想 |

> ⚠️ **C5 是当前最高优先工程债**：必须把 URP 包对齐到 2022.3 对应的匹配版本（v14.x），消除「编辑器 2022.3 + URP v17」的混合态。详见风险 R1 与开放问题 Q3。

---

## 2. 模块划分

按任务要求划分为 **渲染 / 战斗 / 世界 / 经济 / 成长 / 叙事 / UI / 音频接入点** 八大功能模块，外加横切的**基础设施**层。每模块标注职责、代码接入点、与灰盒代码的衔接方式。

| 模块 | 职责 | 代码接入点（目标路径） | 与灰盒衔接 |
|------|------|------------------------|------------|
| **R 渲染与墨韵** | 墨韵全屏 Pass、国风 Toon 着色器、Height Fog、Volume 色调、LOD/合批、性能预算守门 | `Rendering/InkRenderFeature.cs`、`Shaders/InkFullscreen.shader`、`Rendering/ToonGuofeng.shader`（新建）、`Rendering/HeightFog`（Volume） | **直接进生产**：InkRenderFeature + InkFullscreen.shader（已验证，仅加固）；Toon 着色器需新建（S3 最小版缺失） |
| **C 战斗** | 玩家控制/状态机、敌人 AI、伤害/血量系统、闪避/格挡/弹幕对象池、打击感（顿帧/墨溅/震屏/DOF） | `Combat/PlayerCombatController.cs`、`Combat/EnemyEntity.cs`、`Combat/DamageSystem.cs`、`Combat/ProjectilePool.cs`、`Combat/HitFeedback.cs` | **需重构**：PlayerController（灰盒）、DummyEnemy（灰盒）模式可复用，重写为数据驱动状态机 |
| **W 世界与箱庭** | 互锁箱庭关卡数据、模块化区域拼装、路径解锁、重访机制、难度梯度 | `World/BoxartaLevelData`（SO/JSON）、`World/RegionAssembler.cs`、`World/PathUnlock.cs` | **需重构**：GreyboxBuilder 改为关卡数据驱动的 Production 工具（保留为原型器） |
| **E 经济与搜刮** | 不可再生资源池、采集/掉落、死亡掉落惩罚、反通胀曲线 | `Economy/ResourcePool.cs`、`Economy/LootTable.cs`、`Economy/DeathDrop.cs` | 灰盒无对应，全新；接 S 系列采集/掉落行为 |
| **G 成长（炼制+突破）** | 丹/符/器炼制、配方解锁、境界门槛、属性增益、指数门槛、新可行域 | `Growth/CraftingSystem.cs`、`Growth/CultivationSystem.cs`、`Growth/RealmChain.cs` | 灰盒无对应，全新；依赖 E/叙事图鉴 |
| **N 叙事（探索/图鉴/撤退/因果）** | 情报探测（探不可强）、危险预警、撤退遁术、图鉴收编、因果声望/世界生态 | `Narrative/ExplorationSystem.cs`、`Narrative/Codex.cs`、`Narrative/EscapeArt.cs`、`Narrative/KarmaSystem.cs` | 灰盒无对应，全新；探索/撤退接 W 与 C |
| **U UI/HUD** | HUD、菜单、图鉴/炼制/境界 UI、叙事对话、设置 | `UI/`（UI Toolkit 或 UGUI，按 ADR 待定） | 灰盒仅 FpsProbe 的 OnGUI；生产改正式 UI 框架 |
| **A 音频接入点** | 自适应音频、墨韵/法术 sting、环境音、事件驱动触发 | `Audio/AudioEventBus.cs`、`Audio/AdaptiveMixer.cs` | 灰盒无音频；生产新建，C/N/W 通过事件总线触发 |
| **I 基础设施（横切）** | 构建/CI、版本控制与 LFS、存档与数据格式、遥测、输入系统、项目结构 | `Build/`(CI)、`Services/SaveService.cs`、`Services/Telemetry.cs`、`Input/`（New Input System） | **直接进生产**：FpsProbe（遥测底座）、Greybox 菜单工具；CI/存档/输入新建 |

---

## 3. 模块依赖与数据流

### 3.1 依赖关系（自底向上，呼应概念草案 §4）

```
                ┌─────────────┐
                │  I 基础设施  │  (构建/CI·存档·遥测·输入·VCS)  —— 横切全部
                └──────┬──────┘
                       │ 提供入口/数据底座
        ┌──────────────┼───────────────────────────────┐
        │              │                                │
   ┌────▼────┐   ┌─────▼─────┐                  ┌──────▼──────┐
   │ R 渲染   │   │ W 世界箱庭 │                  │  C 战斗      │
   │(墨韵/相机)│   │           │                  │              │
   └────┬────┘   └─────┬─────┘                  └──────┬──────┘
        │              │                                │
        │        ┌─────▼─────┐                         │
        │        │ N 探索     │◄────────────────────────┘ (撤退/情报来自战斗遭遇)
        │        └─────┬─────┘
        │              │
        │        ┌─────▼─────┐
        │        │ E 经济搜刮 │  (不可再生池 + 死亡掉落)
        │        └─────┬─────┘
        │              │
        │        ┌─────▼─────┐   图鉴收编随机→可控
        │        │ N 图鉴    │◄─────────────────────────────┘
        │        └─────┬─────┘
        │              │
        │        ┌─────▼─────┐
        │        │ G 成长     │  (炼制消耗素材 / 突破消耗稀缺)
        │        │ 炼制+突破  │
        │        └─────┬─────┘
        │              │
   ┌────▼────┐  ┌─────▼─────┐  ┌──────────┐
   │ U UI    │◄─┤ N 因果声望 │─►│ A 音频    │  (呈现 + 声音，均由事件驱动)
   └─────────┘  └───────────┘  └──────────┘
        ▲              ▲
        └──── 洞府 Hub（横切锚点：承上搜刮/炼制，启下出击）────┘
```

### 3.2 运行期数据流（单帧 / 单循环视角）

```
玩家输入(New Input) ──► C 战斗(移动/施放/生存)
        │                     │ 命中/受击事件
        │                     ▼
        │              ┌──────────────┐
        │              │ E 经济搜刮    │ 采集/掉落 → 不可再生池 ±
        │              └──────┬───────┘
        │                     │ 遭遇数据
        │                     ▼
        │              ┌──────────────┐   收编 → 记录
        │              │ N 图鉴/因果   │──► 世界生态演进(离线模拟)
        │              └──────┬───────┘
        │                     │ 素材/配方
        │                     ▼
        │              ┌──────────────┐   消耗稀缺 → 境界↑
        │              │ G 成长炼制突破│
        │              └──────┬───────┘
        │                     │ 状态变更
        │                     ▼
   ┌────▼────┐         ┌──────────────┐
   │ I 存档   │◄────────┤ 版本化+加密写盘│  (反作弊/反通胀保护)
   └────┬────┘         └──────────────┘
        │                     │
        ▼                     ▼
   ┌─────────┐         ┌──────────────┐
   │ U UI    │◄────────┤ A 音频事件总线│  (呈现 + 声音反馈)
   └─────────┘         └──────────────┘

R 渲染与墨韵：每帧最后全屏 Pass，读取 C/W 产出的深度+法线，统一国风调性。
```

**关键不变量**：所有经济写入必须经 `E 经济搜刮` 的不可再生池（I6/C6），任何直接改资源的后门都破坏 P1 稀缺即命运。

---

## 4. 关键技术决策与 ADR 补充

### 4.1 已批准 ADR（引用，不重述全文）

- **ADR-001 引擎选型 Unity URP Forward+**：锁 Unity LTS（现 2022.3），禁 Unity 6。
- **ADR-002 渲染管线与后处理**：Volume（色调/雾）+ 单条自定义 Ink Render Feature 全屏 Pass；墨水逻辑抽象隔离引擎 API。
- **ADR-003 资产规范命名 `CAT_SubType_Variant_LOD`**：贴图两级（2K/1K）+ 图集 + Mipmap + 笔触法线 authoring 规范。

### 4.2 新增 ADR

#### ADR-004 构建与持续集成（CI）策略

- **上下文**：小团队（1 主程 + 1 玩法程序 + 0.5 技术美术 + AI 代码生成）；制作人为非程序员，只做环境搭建与试玩反馈。当前无 CI，存在「在我机器能跑」风险；且工程处于「2022.3 编辑器 + URP v17」混合态（见 R1），构建环境必须与锁定版本严格一致，否则墨韵栈会静默漂移。
- **备选**：
  1. *纯本地构建*（无 CI）——零成本但无回归防护，混合态风险无人发现。❌
  2. *Unity Cloud Build*——开箱即用、免运维，但按席位/时长计费、构建排队慢、构建机版本不可控（仍可能拉到非 2022.3），对小团队性价比低。⚠️
  3. **GitHub Actions（或自托管 Gitea Actions）+ GameCI（`unityci/editor`）**——开源免费（自托管 runner 零云成本）、构建机镜像钉死 `unityci/editor:2022.3.xxx-<module>`，天然保证引擎/URP 版本一致；失败时自动跑墨韵回归与帧率冒烟测试。✅
  4. *TeamCity / Jenkins 自托管*——能力强但运维负担重，小团队不划算。⚠️
- **决定**：采用 **GitHub Actions + GameCI**（自托管 Windows runner 优先，避免云分钟计费），构建镜像锁定 2022.3 匹配版本；保留 Unity Cloud Build 作为发行期可选补充（不参与日常 CI）。版本控制用 **Git + Git LFS**（贴图/模型/音频大文件进 LFS）。分支策略：**Trunk-Based**（短生命周期 feature 分支 + 每日向 main 合流），发布用 tag + release branch。
- **后果**：
  - ✅ 构建机版本与本地一致，混合态风险被 CI 卡住；墨韵回归自动化。
  - ✅ 非程序员制作人可通过 GitHub 页面/构建状态看懂「是否可玩」。
  - ⚠️ 需一次性搭建 runner 与 LFS 存储（一次性投入，见开放问题 Q1/Q2）。
  - ⚠️ GameCI 需有效 Unity 许可证（Personal 可跑 CI，但批量构建建议 Plus/Pro 的 CI 授权）。

#### ADR-005 存档与数据格式

- **上下文**：单机买断 PC，含不可再生经济（P1）、图鉴收编（P3）、境界链（长线）。制作人需可读/可调试存档；经济完整性需防本地篡改（反通胀是核心幻想）；游戏静态数据（关卡/配方/敌人表）需让设计侧可编辑。
- **备选**：
  1. *纯 ScriptableObject*——Unity 原生、编辑器友好，但**无版本迁移**、打包后不可改、存档不能序列化为文件，不适合玩家存档。❌
  2. *纯二进制*——体积小、解析快，但**不透明**，制作人无法调试、图鉴/经济问题难排查。❌
  3. **JSON（玩家存档）+ JSON/SO 双轨（静态数据）**，存档 **AES 轻量加密 + 版本号(migration)**——可读可调试、迁移可控、加密保护经济与图鉴完整性。✅
  4. *YAML*——可读但 Unity 生态非原生，序列化库额外依赖。⚠️
- **决定**：
  - **玩家存档**：`SaveService` 写 **版本化 JSON**（schema 带 `saveVersion`，启动做 migration），整体 AES 加密后落盘（`Application.persistentDataPath`）。加密密钥不进仓库（CI 注入）。
  - **静态游戏数据**（关卡/配方/敌人/掉落表/境界曲线）：**ScriptableObject 为主、JSON 导出为辅**——设计在编辑器 SO 调参，构建期可导出 JSON 供调试/平衡；运行时读 SO 或 Addressables 载入的 JSON。
  - **区域流送**：互锁箱庭用 **Addressables** 异步载入（支持重访机制与后续 patch），不进 Resources 文件夹。
- **后果**：
  - ✅ 制作人/设计可肉眼读 JSON 排错；版本迁移防止补丁破坏旧档；加密守住不可再生池与图鉴。
  - ✅ Addressables 让箱庭内容量可摊薄（模块化拼装 + 重访，呼应概念草案 §7 风险缓解）。
  - ⚠️ 需写 migration 层（每次 schema 变更补一个 upgrader）；AES 增加微小 IO 开销（可忽略）。
  - ⚠️ Addressables 引入异步加载复杂度，世界模块需配套加载/卸载状态机。

#### ADR-006 输入系统选型（推荐，待主理人确认）

- **上下文**：灰盒用 `Input.GetAxisRaw/GetKeyDown`（legacy）。生产需键位重绑、手柄支持、施法/闪避的复合输入、以及可调试的输入回放。
- **决定（推荐）**：迁移到 **Unity New Input System**（`PlayerInput` + Input Actions 资产），灰盒 `PlayerController` 重写为数据驱动状态机时一并切换。
- **备选**：保持 legacy Input——零迁移成本但无重绑/手柄/复合输入，长期技术债。
- **后果**：✅ 支持重绑与手柄、输入与逻辑解耦；⚠️ 重写 PlayerController（已在 C 模块重构范畴内，无额外成本）。

---

## 5. 性能预算（呼应 H2）

以灰盒 H2 验收（1080p60，平均 ≥58fps）为硬底线，固定为生产性能契约：

| 维度 | 预算 | 守门模块 | 校验手段 |
|------|------|----------|----------|
| 分辨率/帧率 | **1080p @ 60（平均 ≥58fps，帧时波动 < 8ms）** | R + I(遥测) | FpsProbe 演进为遥测；CI 帧率冒烟 |
| Draw Call | **< 2000**（SRP Batcher + 静态合批 + GPU Instancing） | R(合批) | FpsProbe Draw Calls Count |
| **墨韵栈** | **单全屏 Pass，< 2–3ms**（半分辨率可选） | R(Ink) | 自定义 Profiler 采样 |
| Height Fog | < 1ms（单 Pass，禁真体积 raymarch） | R(Volume) | 自定义 Profiler 采样 |
| CPU 主线程 | < 16ms | 全模块 | ProfilerRecorder |
| 动态光 | 1 主方向光（烘焙 GI）+ 每场景 1–3 实时点/面光（Forward+） | R | 场景规范 |
| 贴图 | 主角/关键 2K，通用 1K；BC7 + Mipmap + 图集 | R(资产) | ADR-003 规范检查 |
| LOD | 环境 3–4 级 / 角色 2–3 级 | R(LOD) | 场景审查 |
| 内存 | 常驻 < 4GB（PC 买断中等体量） | 全模块 | Memory Profiler |
| 加载 | 箱庭切换 < 1.5s（Addressables 异步） | W + I | 加载埋点 |

> 任一预算越线 → 触发性能门禁（CI 失败 + 制作人试玩预警）。墨韵栈与 Draw Call 是最高频越线项，由 R 模块在每里程碑做合批/半分辨率复核。

---

## 6. 风险登记

| ID | 风险 | 类别 | 影响 | 缓解 |
|----|------|------|------|------|
| **R1** | **Unity 版本错配遗留**：「2022.3 编辑器 + URP 被升到 v17」混合态 | 工程/跨版本 | 静默 API 漂移；InkRenderFeature 依赖 `cameraColorTarget` obsolete 路径，混合态下行为可能不一致；某天误开 Unity 6 直接编译失败 | **P0**：对齐 URP 到 2022.3 匹配版本（v14.x），钉死 `manifest.json`；CI 构建机锁版本；写墨韵回归测试（见开放问题 Q3） |
| R2 | API 漂移：升级尝试触发 RenderGraph 改写 | 跨版本 | 整套 Shader/Feature 崩溃 | 锁 LTS（C1）；抽象 `IInkPass` 隔离；回归测试 |
| R3 | 墨水 Pass 过度绘制 / 半分辨率伪影 | 性能 | 帧率跌破 H2 | 单 Pass + 半分辨率 + 严格预算（§5） |
| R4 | 2.5D 深度排序错误 | 渲染 | 角色穿插、可读性崩 | Y-Z 自定义排序轴（C4）+ Sorting Group |
| R5 | cel 描边与屏幕墨线双描边脏化 | 渲染 | 观感像卡通描边 | 统一屏幕空间优先；角色描边交 Ink Pass，关几何描边 |
| R6 | 经济通胀（掉落 ≥ 消耗） | 设计/经济 | 稀释 P1 核心幻想 | 不可再生硬上限 + 死亡掉落 + 境界指数门槛 + 定期经济曲线评审（E 模块） |
| R7 | 箱庭内容量（手工成本高） | 内容 | 小团队难撑体量 | 模块化区域拼装 + 重访机制（W 模块，呼应概念 §7） |
| R8 | 2.5D 手感/打击感弱 | 战斗 | 体验打折 | 墨韵统一调性 + 位移/施放节奏 + 打击感组合（C 模块 S8 延展） |
| R9 | IP 授权（凡人修仙传） | 商业 | 发行受阻 | 内部「去韩立化」开关并行开发；授权谈判前置 |
| R10 | 构建/CI 环境漂移 | 工程 | 混合态风险复发 | ADR-004：自托管 runner 钉版本 + LFS + trunk-based |
| R11 | 存档被本地篡改击穿经济 | 安全 | 不可再生池失真 | ADR-005：AES 加密 + 版本迁移 + 经济写入单点（E 模块） |

---

## 7. 原型代码进生产评估

**结论先行**：墨韵栈（R 模块）与相机/遥测/灰盒工具（I 模块）已验证，直接进生产；战斗与世界的灰盒脚本需重构（模式可复用）；Toon 着色器与多数系统层为全新。

| 原型文件 | 模块 | 进生产判定 | 处理 | 说明 |
|----------|------|-----------|------|------|
| `Rendering/InkRenderFeature.cs` | R | ✅ **直接进生产** | 仅加固（参数校验/回归测试） | 已用单路径 CommandBuffer API 重写，跨版本安全；严守 C2/C5 |
| `Shaders/InkFullscreen.shader` | R | ✅ **直接进生产** | 仅调参/扩展 | 程序化噪声+深度 Sobel+纸纹+渍墨+飞白已验证；补「法术写意」笔触（概念 H 清单 5） |
| `Camera/CameraRig.cs` | R/C | ✅ **直接进生产** | 扩展多目标 framing | 固定斜 45°/禁旋转已验证（S1）；补 H5 箱庭构图（目标组跟随） |
| `Core/FpsProbe.cs` | I | ✅ **直接进生产底座** | 演进为遥测 HUD | ProfilerRecorder 跨版本安全（S9）；生产改正式调试叠层+CI 冒烟 |
| `Core/GreyboxBuilder.cs` | W/I | 🔶 **保留为工具，需重构为数据驱动** | 抽出「关卡原型器」 | 互锁箱庭布局逻辑可复用（S2）；生产改读 `BoxartaLevelData` 生成 |
| `Editor/GreyboxMenu.cs` | I | ✅ **直接进生产（工具）** | 保留 | 菜单入口合规（Editor 文件夹） |
| `Editor/InkMaterialCreator.cs` | I | ✅ **直接进生产（工具）** | 保留 | 一键生成墨韵材质，省去制作人手动步骤 |
| `Player/PlayerController.cs` | C | 🔴 **需重构** | 重写为 `PlayerCombatController` | 灰盒 WASD+闪避 i 帧+普攻模式**可复用**；改 New Input + 状态机 + 动画/事件钩子 + 数据驱动数值（ADR-006） |
| `Enemy/DummyEnemy.cs` | C | 🔴 **需重构** | 重写为 `EnemyEntity` + 战斗 AI | 浮空/命中闪红/`TakeHit` 模式**可复用**；接伤害系统、Z 分层 AI、巡逻/索敌 |
| `Toon 国风着色器`（S3 最小版） | R | ❌ **缺失，需新建** | 新建 `ToonGuofeng.shader` | 灰盒无国风着色器；需 Toon Ramp + Rim + 笔触法线 + 屏幕墨线（技术评估 §5.1） |
| Height Fog / Volume（S7） | R | 🔶 **配置进生产** | 固化 Volume Profile | 灰盒已在 URP Volume 配；生产固化 Height Fog 单 Pass + Color Grading |
| 打击感（S8 顿帧/墨溅/震屏/DOF） | C | 🔴 **仅占位，需实现** | 新建 `HitFeedback` | 灰盒无实现；生产做顿帧时间缩放 + 墨溅粒子 + 震屏 + DOF 焦点 |

---

## 8. 待拍板开放问题（汇总，详见 `sprint-plan.md` 末尾）

1. **Q1 CI/构建工具选型**：确认 GitHub Actions + GameCI 自托管 runner（推荐）还是 Unity Cloud Build？
2. **Q2 VCS 与 LFS 托管**：GitHub / Gitea 自托管 / 其他？LFS 存储预算？
3. **Q3 URP 版本对齐**：确认将 URP 从 v17 降回 2022.3 匹配版本（v14.x）以消除混合态（推荐），还是维持现状并仅靠 `cameraColorTarget` 兼容路径？
4. **Q4 静态数据格式**：SO 为主还是 JSON 为主？设计侧编辑工具谁来维护？
5. **Q5 输入系统**：确认迁移 New Input System（推荐，ADR-006）？
6. **Q6 团队规模/冲刺速率**：当前排期假设 2 工 + 0.5 技术美术 + AI 代码生成，约 30 SP/2 周冲刺；请确认实际产能。
7. **Q7 反作弊范围**：单机买断是否需 AES 存档加密（推荐，守 P1）？

---
*下接 `production/roadmap/sprint-plan.md`：Epic/Story 拆分、故事点、冲刺排期、灰盒衔接点。*
