# 战斗状态机设计文档 — 玩家控制重写（E3-S1）

> **文档 ID**：`design/gdd/system-combat-playercontroller.md`
> **所属 Epic / Story**：E3 战斗核心系统 · **E3-S1 玩家控制重写**（8 SP）
> **冲刺归属**：S3 战斗骨架（最大 Story，前置设计）
> **作者**：design-strategist（文策渊）
> **状态**：**DRAFT · 待制作人拍板**（数值为占位/可配，未经 playtest 校准）
> **受众（双轨）**：
> - **工程主程（程基岩）**：Part B / C / D / E 为实现蓝图（状态机、数值 schema、输入契约、可测验收点）。
> - **制作人（非程序员）**：Part 0 / A 为设计意图（大白话讲「玩家能做什么、手感目标是什么」）。
> **关联文档**：`docs/architecture/production-architecture.md`(ADR-006)、`production/roadmap/sprint-plan.md`(E3/S3)、`Assets/Scripts/Player/PlayerController.cs`(灰盒复用)、`Assets/Scripts/Enemy/DummyEnemy.cs`(下游 E3-S2/S3)。
> **实现纪律**：验证驱动开发（先测后写）；本文档不写游戏代码、不 git commit；待制作人确认后由工程主程实现。

---

## Part 0 — 给制作人的一页纸（大白话）

**玩家在 E3-S1 能做什么？**
- **走动**：WASD（相机相对，固定斜 45° 视角，W=往屏幕里走）。和现在灰盒一样。
- **普攻**：鼠标左键（或手柄 X 键）劈一刀，打身边一圈敌人。E3-S1 先做「单段普攻」，连段/轻重击留到后面功法系统。
- **闪避翻滚**：空格（或手柄 B 键）翻滚一下，翻滚瞬间有**无敌帧**（敌人打不中你），翻滚有短冷却。这就是「撤退遁术」手感的雏形——让玩家觉得「我能躲掉」。
- **挨打硬直**：被击中且不在无敌帧时，会愣一下（硬直），硬直结束回到可控。
- **死亡**：血没了就死，死亡掉落交给后面经济系统处理（本设计只留钩子）。

**手感目标是什么？（一句话）**
> 让玩家**感到自己能躲、能打、进退有度**——核心体验是「**信息差 > 硬刚**」：玩家可以选择绕后偷袭、可以翻滚撤退，而不是无脑站桩对拼。稀缺张力（资源有限）和箱庭互锁（路被锁/被开）在后面系统承接，本 Story 先把「可控的战斗手感」地基打好。

**为什么这么做（给制作人看的决策逻辑）**
- 状态机只放 6 个状态（待机/移动/普攻/闪避/硬直/死亡），**不多加**，避免玩家脑子过载（心流）。
- 闪避带无敌帧 + 冷却，是「胜任感」的来源：新手靠它活命，高手靠它秀操作。
- 所有数值（速度、伤害、冷却、无敌时长）都做成**可配表格**，不在代码里写死——后面「突破成长（E9）」会往里注入增益，本设计只留接口。
- 不碰经济（不掉任何东西、不增任何资源），严守「反通胀」红线。

---

## Part A — 设计意图（MDA / 心流 / 自我决定论 / Bartle）

> 本节把设计哲学落到 E3-S1 的具体取舍上，供制作人判断「设计是否自洽」，也供主程理解「为什么是这样而不是那样」。

### A.1 核心体验定位（roadmap §7 红线自检对齐）
- **信息差 > 硬刚**：玩家应能「打得过就打、打不过就溜」。闪避/撤退的代价低、收益明确，使战斗成为「选择」而非「被迫对拼」。
- **稀缺张力**：本 Story 不消耗/产出稀缺资源，但数值与冷却节奏要为后续「突破消耗稀缺」留口（E9 注入增益，不硬编码）。
- **箱庭互锁有趣**：玩家控制本身不实现箱庭，但移动/闪避的「可撤退性」是箱庭遭遇可读性的前提（接 E2 相机 / E5 探索预警）。

### A.2 MDA 框架拆解
| 层 | E3-S1 落点 |
|----|-----------|
| **Mechanics（机制）** | 6 状态有限状态机 + 相机相对移动 + 普攻范围判定 + 闪避无敌帧/冷却 + 数据驱动数值表 |
| **Dynamics（动态）** | 玩家行为 = 进退有度：探→撤→偷袭→翻滚脱战；冷却管理形成节奏；信息差下「可逃」降低焦虑、「可打」提供正反馈 |
| **Aesthetics（美学/体验）** | 感觉（Sensation）+ 挑战（Challenge）+ 发现（Discovery）：打击有冲击（H4 前置接口）、躲闪有爽感、箱庭探索有未知张力 |

### A.3 自我决定论（SDT）映射
- **自主 Autonomy**：Move/Attack/Dodge 自由组合，玩家自定进退策略；信息差体验让「逃」成为合法且聪明的选择。
- **胜任 Competence**：i 帧 + 闪避冷却让玩家「我能躲过那一下」；后续打击感（E3-S5）反馈强化胜任感。
- **关联 Relatedness**：本 Story 不直接处理，由撤退遁术（E10）/图鉴收编（E7）/因果声望（E11）承接「与世界的关系」。

### A.4 心流与认知负荷
- 状态数量克制（6 个），避免 Bartle 中 Explorer/Achiever 在战斗上面脑过载。
- 难度—技能平衡由「i 帧窗口宽度 × 冷却时长 × 硬直时长」共同调节：窗口过窄→新手劝退；过宽→无敌策略（违反 P2 无主导策略，见 A.6）。

### A.5 Bartle 玩家类型覆盖
- **Achiever / Killer（战斗精通）**：普攻/闪避连携、无伤通关追求 → 由数值与冷却节奏服务。
- **Explorer（信息差/探索）**：可撤退、可绕后 → 由「撤」的低代价与箱庭可读性服务（接 E5）。
- **Socializer**：本 Story 不重点服务，由 E11 因果声望承接。

### A.6 红线与约束（写死，不可逾越）
- **P1 反通胀**：本设计**不新增任何掉落/资源、不写任何经济写入**。玩家控制纯「动作层」，与经济池零耦合。
- **数值公式不擅改**：本文只给出**占位字段 + 建议范围/默认**，不定义新数值公式；任何公式改动须回传主理人。
- **E9 增益走数据驱动注入**：玩家成长增益（移速/伤害/无敌时长等）由 E9 突破系统在运行时注入，本设计**只留 hook 字段**，不在本表硬编码任何成长曲线。
- **验证驱动**：先写验收点（Part E），再由工程主程实现 + 测试。

---

## Part B — 战斗状态机设计（工程蓝图）

### B.1 状态枚举与入选理由
| 状态 | 含义 | 入选理由 | 不在本 Story 实现的近邻状态 |
|------|------|----------|------------------------------|
| **Idle** | 静止待机 | 无输入基线态，所有循环回归点 | — |
| **Move** | 相机相对移动（XZ 平面，Y 锁地） | 复刻灰盒 WASD；2.5D 固定相机下相机相对映射 | — |
| **Attack** | 普攻（范围命中标记） | 复刻灰盒左键 OverlapSphere；E3-S1 做单段 | 连段 Combo（留后续功法 E17）、Spell 施法（更后期） |
| **Dodge** | 闪避翻滚（带 i 帧 + 冷却） | 复刻灰盒空格；「撤退遁术」雏形；i 帧是胜任感核心 | Block/Parry 格挡（E3-S4）、弹幕（E3-S4） |
| **HitStun** | 受击硬直（非无敌时） | 受击反馈与节奏控制；后续打击感（E3-S5）接此态 | 击飞/浮空（玩家固定地面，不适用） |
| **Dead** | 死亡终态 | hp≤0（由 E3-S2 触发）钩子；回 hub 由 E10/E12 处理 | Respawn（E12 范围，仅留钩子） |

> **为何不加 Block/Spell/Stagger 等状态**：格挡/弹幕属 **E3-S4**，施法属更后期功法；Stagger 可归为 HitStun 变体。E3-S1 刻意只做「最小可玩战斗骨架」，但 **B.4 预留状态可扩展接口**（新增状态不破坏既有转换）。

### B.2 状态转换条件表（事件 → 源状态 → 目标状态 → 守卫）
事件定义：
- `EVT_MOVE_INPUT`　移动输入非零
- `EVT_MOVE_STOP`　移动输入归零
- `EVT_ATTACK_PRESSED`　Attack action 触发（按下）
- `EVT_DODGE_PRESSED`　Dodge action 触发（按下）
- `EVT_DODGE_END`　闪避时长耗尽
- `EVT_HIT_RECEIVED`　受击事件（由 E3-S2 伤害系统广播，含无敌判定）
- `EVT_HITSTUN_END`　硬直时长耗尽
- `EVT_DEATH`　hp≤0（由 E3-S2 触发）

| # | 事件 | 源状态 | 目标状态 | 守卫条件 |
|---|------|--------|----------|----------|
| T1 | EVT_MOVE_INPUT | Idle | Move | — |
| T2 | EVT_MOVE_INPUT | Attack（已结束） | Move | 普攻完成（attackDuration 耗尽） |
| T3 | EVT_MOVE_STOP | Move | Idle | — |
| T4 | EVT_ATTACK_PRESSED | Idle | Attack | `attackCooldown <= 0` |
| T5 | EVT_ATTACK_PRESSED | Move | Attack | `attackCooldown <= 0` |
| T6 | EVT_ATTACK_PRESSED | Attack | （缓冲/忽略） | E3-S1 MVP：**忽略**（连段留后续）；可选缓冲下一击，见 A.6 Q1 |
| T7 | EVT_ATTACK_PRESSED | Dodge / HitStun / Dead | （忽略） | 翻滚/硬直/死亡中不可普攻 |
| T8 | EVT_DODGE_PRESSED | Idle | Dodge | `dodgeCooldown <= 0` |
| T9 | EVT_DODGE_PRESSED | Move | Dodge | `dodgeCooldown <= 0` |
| T10 | EVT_DODGE_PRESSED | Attack | Dodge | `dodgeCooldown <= 0`（允许 attack-cancel 进闪避，降低硬直惩罚） |
| T11 | EVT_DODGE_PRESSED | HitStun / Dead | （忽略） | 硬直/死亡中不可闪避 |
| T12 | EVT_DODGE_END | Dodge | Idle（或 Move 若有输入） | i 帧随翻滚结束处理，见 B.5 边缘情况 |
| T13 | EVT_HIT_RECEIVED | 任意（非 Dead） | HitStun | **非无敌**（`iFrameTimer <= 0`）且 `hp > 0` |
| T14 | EVT_HIT_RECEIVED | Dodge / 任意 | （吸收，无转移） | **无敌**（`iFrameTimer > 0`）→ 不掉血、不进 HitStun |
| T15 | EVT_HITSTUN_END | HitStun | Idle（或 Move 若有输入） | — |
| T16 | EVT_DEATH | 任意（非 Dead） | Dead | `hp <= 0`（来自 E3-S2）— **优先于 T13**（死亡直接进 Dead，跳过 HitStun） |
| T17 | （预留）EVT_RESPAWN | Dead | Idle | **超出 E3-S1 范围**（E12 Hub）；仅留钩子 |

> **状态图（ASCII）**
> ```
>            EVT_MOVE_INPUT            EVT_ATTACK_PRESSED(guard: cd)
>   Idle ─────────────► Move ──┐   ┌──────────────► Attack ──(结束)──┐
>     ▲                        │   │                                  │
>     │ EVT_MOVE_STOP          │   │ EVT_DODGE_PRESSED(guard: cd)      │
>     └────────────────────────┘   ▼                                  ▼
>                              Dodge ◄──────────────────────────── Move/Idle
>                              │ ▲  EVT_DODGE_END                       │
>        EVT_HIT_RECEIVED     │ │ i帧内吸收 / i帧外→HitStun            │
>        (非无敌,hp>0)         │ └─────────────────────────────────────┘
>        ┌──────────────► HitStun ──(结束)──► Idle/Move
>        │
>   EVT_DEATH(hp<=0, 优先) ──► Dead (终态, 钩子→E12)
> ```

### B.3 与 New Input System（ADR-006）的 Input Action 映射草图
> 前置：E0-S4 已交付 New Input System 基底（包启用 + `Input Actions` 资产骨架 + `PlayerInput` 挂在玩家预制体 + 灰盒临时映射可玩）。**E3-S1 消费该基底，定义战斗语义并彻底移除 legacy `Input.GetAxisRaw/GetKeyDown`。**

**Input Actions 资产草图（Action Map：`PlayerCombat`）**
| Action 名 | 类型 | 绑定（默认，可重绑由 E13 UI 承接） | 触发时机 | 映射到状态机事件 |
|-----------|------|--------------------------------------|----------|------------------|
| `Move` | `Vector2`（Value） | WASD / 左摇杆 | 持续 | `EVT_MOVE_INPUT` / `EVT_MOVE_STOP` |
| `Attack` | `Button`（Press） | 鼠标左键 / 手柄 X / 键 J | 按下 | `EVT_ATTACK_PRESSED` |
| `Dodge` | `Button`（Press） | 空格 / 手柄 B / 键 K | 按下 | `EVT_DODGE_PRESSED` |
| `Spell`（预留，不实现） | `Button` | 鼠标右键 / 手柄 Y | — | 仅占位，E3-S1 不订阅 |
| `Block`（预留，不实现） | `Button` | 鼠标右键 / 手柄 LB | — | 仅占位（E3-S4） |
| `Pause`（预留） | `Button` | Esc | — | 交 E13 菜单，不进战斗状态机 |

**接线方式（描述，不写代码）**
- `PlayerInput` 通过 `onActionTriggered` / `performed` 回调把抽象事件推给状态机；**状态机只认事件名，不认设备**。
- 切换键鼠↔手柄行为一致（验收点 E.6）。
- 重绑数据存盘走 ADR-005，UI 由 E13 提供；本设计只保证数据结构支持重绑。

### B.4 状态机实现契约（接口/生命周期，供主程落地）
> 不写实现代码，只定义**契约**，保证可测、解耦动画、顿帧不卡逻辑。

- **模式**：推荐**手写状态模式（State Pattern）**，不依赖 Animator 状态机驱动。理由：计时器用 `unscaledDeltaTime` 独立累加（见 B.5），与表现层时间缩放（顿帧）解耦；逻辑可单元可测；动画仅作状态的表现订阅。
- **生命周期钩子（每个状态建议）**：`OnEnter` / `OnUpdate(dt)` / `OnExit`；状态机持有 `currentState` 与全局计时器（`iFrameTimer` / `attackCdTimer` / `dodgeCdTimer`）。
- **对外暴露接口（供下游调用）**：
  - `bool IsInvincible` — E3-S2 受击判定用。
  - `void ApplyHitStun(float duration)` — E3-S2 调用，触发 `EVT_HIT_RECEIVED`→HitStun。
  - `void TakeHit(float damage)` — E3-S2 调用（伤害结算在 E3-S2，本类只转发/钩子）。
  - `event Action OnDeath` — E3-S2 置 hp≤0 后广播，供 E10/E12 接回 hub。
- **对外广播事件（供 E3-S5 打击感订阅，本 Story 不实现打击感）**：
  - `OnAttackStarted` / `OnAttackLanded`（命中瞬间）/ `OnDodgeStarted` / `OnHitReceived`（被击瞬间）。

### B.5 边缘情况（Edge Cases，至少 3 类，须有处理定义）
1. **EC-1 i 帧内受击**：`EVT_HIT_RECEIVED` 且 `iFrameTimer>0` → 吸收，不进 HitStun、不掉血（T14）。**测试必覆盖**。
2. **EC-2 i 帧与翻滚时长关系**：灰盒 `dodgeDuration=0.25s` < `dodgeInvincibleTime=0.3s`，即**无敌帧略长于翻滚**。决策待拍板（Q3）：建议保留「i 帧略长于翻滚」→ 翻滚结束回 Idle 后仍有无敌余量，手感更宽容；此时 `EVT_DODGE_END` 不清除 i 帧，i 帧独立计时到 0。
3. **EC-3 死亡与硬直竞态**：同一次受击使 `hp<=0` → 直接 `Dead`（T16 优先于 T13），**不先进 HitStun 再死**。
4. **EC-4 暂停/失焦**：`OnApplicationPause` / 时间缩放期间，状态机计时器用 `unscaledDeltaTime` 累加并冻结，恢复后连续，不丢 i 帧/冷却。
5. **EC-5 硬直中收到闪避输入**：忽略（T11），避免「被硬直还能翻滚」破坏打击感与平衡。
6. **EC-6 冷却中连点普攻/闪避**：忽略，不重复进入状态、不刷新计时器。

---

## Part C — 数据驱动数值 Schema 草案

> **准则**：所有字段**从 ScriptableObject / JSON 载入**（接 ADR-005），**无 Inspector 硬编码魔法数字**。下文「建议范围/默认」为占位，待 S3 末试玩校准；**不定义新公式**，不擅改既有经济公式。

### C.1 玩家战斗数值字段表
| 字段名 | 单位 | 建议范围 / 默认占位 | 说明 | E9 突破增益注入 |
|--------|------|----------------------|------|------------------|
| `moveSpeed` | m/s | 默认 5（灰盒）、范围 3.5–7 | 相机相对移动基速 | 由 E9 注入 `moveSpeedMul`（乘区） |
| `moveAccel` | m/s² | 范围 30–60（占位） | 趋向目标速度的加速度（手感软硬） | 可选 E9 注入 |
| `moveDecel` | m/s² | 范围 30–60（占位） | 松手减速 | 可选 E9 注入 |
| `dodgeSpeed` | m/s | 默认 14（灰盒）、范围 10–20 | 翻滚位移速度 | 可选 E9 注入 |
| `dodgeDuration` | s | 默认 0.25（灰盒）、范围 0.2–0.35 | 翻滚持续 | 可选 E9 注入 |
| `dodgeCooldown` | s | 默认 0.8（灰盒）、范围 0.5–1.2 | 翻滚冷却（防无敌刷） | 由 E9 注入 `dodgeCooldownMul`（乘区） |
| `iFrameDuration` | s | 默认 0.3（灰盒）、范围 0.2–0.4 | 无敌帧时长（可 > 翻滚，见 EC-2） | 由 E9 注入 `iFrameDurationAdd`（加区） |
| `attackDamage` | HP 单位 | 默认 34（灰盒）、范围 20–50 | 普攻伤害**配置值**，实际结算在 E3-S2 | 由 E9 注入 `attackDamageAdd`/`Mul` |
| `attackRange` | m（半径） | 默认 2.5（灰盒）、范围 1.8–3.5 | OverlapSphere 半径 | 可选 E9 注入 |
| `attackInterval`/`attackCooldown` | s | 默认 0.4（灰盒）、范围 0.3–0.6 | 普攻冷却 | 可选 E9 注入 |
| `attackDuration` | s | 默认 ~0.4、范围 0.3–0.6 | 起手+生效+收招总时长（控制可取消窗口） | 可选 E9 注入 |
| `attackArc`/`attackAngle` | ° | 占位（MVP 用球型，不限角度） | 后续可改锥形；E3-S1 用灰盒球型 | 可选 E9 注入 |
| `hitStunDuration` | s | 默认 0.35、范围 0.2–0.5 | 受击硬直时长（实际由 E3-S2 按敌数据应用） | — |
| `hitKnockback` | m/s（脉冲） | 默认 0（灰盒无击退）、占位 | 击退表现；视觉属 E3-S5 打击感 | — |
| `maxHp` | HP | 默认 100（参考灰盒敌）、占位 | 玩家血量上限（E3-S2 持有，本表留配） | **强依赖 E9**：由突破注入 `maxHpAdd` |
| `respawnInvuln` | s | 占位（默认 1.0） | 复活后保护无敌（E12 范围，钩子） | 可选 E9 注入 |

> **E9 hook 约定（统一）**：本表所有带「由 E9 注入」字段，运行时由 `CultivationSystem`（E9）在突破结算后写入玩家数值容器；`PlayerCombatController` 读取**合成后**的最终值，本设计**不硬编码任何成长曲线**。`maxHp` / `attackDamage` / `iFrameDuration` / `dodgeCooldown` 为强 hook 字段，必须走注入通道，禁止在本地写死成长。

### C.2 数据载体
- 主载体：`PlayerCombatProfile`（ScriptableObject），编辑器可调参；构建期可按 ADR-005 导出 JSON 供平衡/调试。
- 运行时：载入 SO 或 Addressables JSON，注入 E9 增益后得到「生效数值快照」。
- 制作人可读：JSON 明文（ADR-005），便于非程序排错。

### C.3 数值不写死示例（给主程的约定）
- 错误：`if (hp < 50) ...` 魔法数字。
- 正确：所有阈值/速率来自 `PlayerCombatProfile` 字段；公式维持既有（不在此定义新公式）。

---

## Part D — ADR-006 衔接说明

### D.1 迁移方式
- **E0-S4（已计划前置）** 完成：启用 New Input System 包、建 `Input Actions` 资产骨架、`PlayerInput` 挂玩家预制体、灰盒临时映射可玩。
- **E3-S1** 完成：在 `PlayerCombat` Action Map 定义 `Move/Attack/Dodge` 语义（B.3），订阅回调→状态机事件，**彻底删除** `PlayerController.cs` 中 `Input.GetAxisRaw/GetKeyDown` 等 legacy 调用。

### D.2 临时映射如何处理
- E0-S4 的灰盒临时映射在 E3-S1 被**正式绑定覆盖**（键鼠 + 手柄默认方案），legacy 路径删除，无双轨残留。
- 重绑 UI 不在本 Story（交 E13），但数据结构（Input Actions 资产）天然支持运行时重绑，存档走 ADR-005。

### D.3 是否 E3-S1 一次性到位
- **是**：输入系统切换与战斗状态机重写**一次性到位**（ADR-006 决定原文：「灰盒 PlayerController 重写为数据驱动状态机时一并切换」）。E3-S1 收尾后，项目内**不再有 legacy Input 依赖**于玩家控制。

---

## Part E — 可验证验收点（供工程主程写测试 · 验证驱动）

> 每条给出可测断言（Given/When/Then 风格），对应「先测后写」。范围严格限定 E3-S1；下游（E3-S2/S3/S4/S5）仅验接口契约。

**E.1 状态机不卡死（必过）**
- Given 任意初始状态，When 注入 1000 个随机合法事件序列，Then 状态机始终处于 6 态之一、无异常、最终可回到 Idle/Move；Dead 为终态不可再转移（除预留 Respawn 钩子）。

**E.2 i 帧正确性（必过，接 EC-1）**
- Given 处于 Dodge 且 `iFrameTimer>0`，When 收到 `EVT_HIT_RECEIVED`，Then 不进 HitStun、`TakeHit` 不被结算（hp 不变）。
- Given `iFrameTimer<=0` 且 `hp>0`，When 收到 `EVT_HIT_RECEIVED`，Then 进入 HitStun。

**E.3 冷却正确性**
- Given `dodgeCooldown>0`，When `EVT_DODGE_PRESSED`，Then 被忽略、不进 Dodge、不刷新计时器。普攻冷却同理（T6/T11 类）。

**E.4 数据驱动正确性（接 C）**
- Given 配置表 `attackDamage`/`moveSpeed` 等从 SO/JSON 载入，When 修改配置值，Then 运行时生效（热重载或重启后），**代码内无硬编码魔法数字**（静态检查/Code Review 断言）。

**E.5 打击感前置接口（H4 hook，不实现打击感本身）**
- Given 普攻命中 / 闪避开始 / 被击，When 对应时刻，Then 广播 `OnAttackLanded`/`OnDodgeStarted`/`OnHitReceived` 事件，E3-S5 `HitFeedback` 可订阅。
- **顿帧不卡逻辑（硬指标）**：状态机所有计时器（`iFrameTimer`/`attackCdTimer`/`dodgeCdTimer`/`hitStunTimer`）使用 `unscaledDeltaTime` 累加；当 E3-S5 触发全局顿帧（Time.timeScale 缩放）时，上述计时器**不受影响**，i 帧/冷却/硬直不漂移。

**E.6 输入解耦**
- Given 同一套抽象事件，When 分别用键鼠与手柄触发，Then 状态转移行为完全一致；状态机不引用任何具体设备 API。

**E.7 相机相对移动（2.5D 固定斜 45°）**
- Given 固定斜 45° 相机（禁旋转），When 按 W，Then 玩家朝「远离相机」方向移动；A/D 对应屏幕左右，映射与灰盒一致（无旋转偏差）。

**E.8 下游接口契约（供 E3-S2/S3/S4/S5 联调）**
- `IsInvincible` / `ApplyHitStun(duration)` / `TakeHit(damage)` / `OnDeath` 接口存在且语义如上（B.4）；E3-S2 伤害系统可正确驱动 HitStun 与 Dead。

---

## Part F — S3 其他 Story 的设计输入判定（工程/工具项，交 eng-lead）

> 依据任务范围：**以下 S3 冲刺内 Story 属工程性能/工具或美术，design-strategist 不产出设计文档**，由对应负责人计划。本文档不在其展开。

| Story | 内容 | 归属 | 本设计处理 |
|-------|------|------|------------|
| **E1-S4 写意笔触** | 施法走 Ink Pass 一笔写意 | **art-director**（渲染写意笔触扩展） | 本设计不涉及 |
| **E1-S5 合批 / LOD 守门** | SRP Batcher + 静态合批 + GPU Instancing + LOD | engineering-lead | 无需 design-strategist 产出，交 engineering-lead 计划 |
| **E2-S3 排序轴 gizmo** | 编辑期 Y-Z 排序可视化 | engineering-lead | 无需 design-strategist 产出，交 engineering-lead 计划 |
| **E1-S6 半分辨率守门** | 墨韵栈 <2–3ms；CI 性能门禁 | engineering-lead | 无需 design-strategist 产出，交 engineering-lead 计划 |
| **E0-S6 遥测脚手架** | FpsProbe→Telemetry（DrawCall/帧时/墨韵耗时） | engineering-lead | 无需 design-strategist 产出，交 engineering-lead 计划 |

**同冲刺接口边界（E3-S1 ↔ 其他 S3 Story）**
- E3-S1 与 **E1-S4（写意）**：普攻/闪避的表现层（写意笔触）由 E1-S4 在 Ink Pass 承接，E3-S1 只广播事件（E.5），不依赖其实现时序。
- E3-S1 与 **E1-S5/E1-S6/E2-S3/E0-S6**：纯性能/工具，互不阻塞；E3-S1 只需保证自身 CPU 主线程 < 预算（状态机轻量，无 GC 分配热点）。

---

## Part G — 待制作人拍板的设计抉择（Open Questions）

| # | 问题 | 推荐方案 | 影响 |
|---|------|----------|------|
| **Q1** | 普攻是否连段（combo）？ | E3-S1 **MVP 单段普攻**，连段/轻重击留功法系统（E17） | 决定 Attack 状态复杂度；推荐单段降低风险 |
| **Q2** | 是否预留 Spell/Block 状态接口？ | **预留接口不实现**（B.1/B.3 占位），格挡交 E3-S4、施法交后续 | 决定状态机扩展性，推荐预留 |
| **Q3** | i 帧可否长于翻滚？（灰盒 0.3>0.25） | **保留「i 帧略长于翻滚」**（EC-2），手感更宽容 | 影响无敌余量手感，推荐保留 |
| **Q4** | 状态机实现方式 | **手写 State Pattern**（B.4），不依赖 Animator | 决定可测性与顿帧解耦，推荐手写 |
| **Q5** | 数值默认占位校准 | 占位须经 **S3 末试玩**校准（roadmap 关键节点 S3 末试玩战斗手感） | 所有 C.1 范围为草案，待 playtest |

> **红线自检（本文档）**：✅ 未新增任何经济写入（守 P1 反通胀）；✅ 未定义/改动数值公式（仅占位范围）；✅ E9 增益全部走注入 hook，未硬编码成长；✅ 状态数量克制（守心流/无认知过载）；✅ 未触及 IP 红线。本文档仅做战斗控制设计，不写游戏代码、不 git commit。

---
*回传主理人（team-lead）：文档路径 + 状态机要点 + 待拍板项见 SendMessage。*
