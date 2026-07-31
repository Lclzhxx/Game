# Phase 5 · 设计决策落盘汇编（2026-07-29）

> 主理人：游承峰 ｜ 落盘成员：文策渊（design-strategist）｜ 状态：一致性 PASS、未触红线

## 一、本次落地的 4 项用户决策

### 1. 死亡掉料重置机制（已闭合）
- 规则：死亡掉落比例按死亡次数动态递减 `[50/40/30/20%地板]`，**计数器 N 在「完成关键任务」时重置回 50%**。
- 触发重置的关键任务：**T2 筑基完成 / T5 结丹突破 / T8 化神临界**。
- 落地：`design/gdd/detail-vertical-slice.md` §3.2、`design/reference/人界篇-content-pack.md` §5.5 + §6.2#1。

### 2. 坊市争斗系统 + 叙事张弛（已闭合）
- 新建 `design/gdd/system-15-坊市争斗-marketplace-combat.md`：坊市=经济/声望枢纽；资源争夺（竞标/抢购/黑市）；正/灰/魔三态声望对抗（呼应 system-10 karma）；PvPvE 限时夺宝/帮派火并；对接 T3 任务链；MVP 切片仅 1 个坊市事件原型。
- `design/gdd/system-13-叙事任务-narrative-quest.md` 新增「叙事节奏：张弛交替」：激烈事件（坊市争斗/秘境夺宝/Boss/火并）间必须插舒缓过渡（游历/访谈/洞府经营/轻支线）；节奏模板：紧张段 ≤15–25min → 其后缓和段 ≥ 紧张段 ×0.6。
- **v1 第二条箱庭定为 T3 坊市争斗**。

### 3. 境界命名：结丹正式 / 金丹分支（已闭合）
- 正式境界名 = **结丹**；**金丹 = 结丹期主流分支（子类型）**。
- 落地：content-pack §0/§4.1/§5.3（补主流「凝结丹」+ 保留金丹分支「凝金丹」）、system-06 §②§③§⑥、MVP-scope/INDEX/novel-brief 同步。

### 4. 化神定位：灵界/仙界 + 第一阶段终点（已闭合）
- 化神境界主舞台在**灵界/仙界**，作为**游戏第一阶段最终目标终点**。
- 落地：content-pack §0/§2 T8、system-06 §②§⑥、system-13 §③§⑥、INDEX/MVP-scope/`design/game-concept.md` 同步「v1 止于化神、v2 = 灵界仙界篇（第二阶段）」。

## 二、一致性结论
- 跨文档自检 = **PASS**：结丹/金丹、化神终点、死亡重置三套口径在所有相关文件一致。
- **红线未触碰**：P1 反通胀（掉落<消耗、Cap_SM）、境界指数门槛（`CR_req(n)=CR0·1.8^(n-1)`）均未被改动。

## 三、遗留风险与待办（CONCERNS）
- **C1（排期冲击，建议主理人知会）**：决策 4 实质把 v1 从「3 境链」扩到「5 境链（止于化神）」，新增元婴/化神两段境界 + 内容/任务/技能。**v1 工作量与时程需重评**（建议重跑 sprint-plan）。
- **C2（已批准上游被改，需用户确认）**：为保一致，文策渊扩展了已批准的 `design/game-concept.md` v1/v2 范围表述。请确认是否认可修改已批准上游文档。
- C3：system-15 尚未进 MVP-scope 依赖图（非阻断，程序排期时补）。
- C5：黑市「软货币」引入仍为开放问题，须过 P1 反通胀评审。

## 四、改动文件清单
- `design/gdd/detail-vertical-slice.md`（§3.2）
- `design/reference/人界篇-content-pack.md`（§0 / §2 T3,T5,T8 / §4.1 / §5.3 / §5.5 / §6.2 #1–#4）
- `design/gdd/system-06-突破成长-cultivation.md`（§② / §③ / §⑥）
- `design/gdd/system-13-叙事任务-narrative-quest.md`（§③ / §⑥ / 新增叙事节奏节）
- `design/gdd/system-15-坊市争斗-marketplace-combat.md`（**新建**）
- `design/gdd/MVP-scope.md`、`design/gdd/INDEX.md`、`design/reference/novel-reference-brief.md`、`design/game-concept.md`
