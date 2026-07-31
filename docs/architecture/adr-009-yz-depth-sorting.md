# ADR-009 Y-Z 自定义深度排序轴（E2-S2）

> 状态：**提议**
> 关联：C3（固定斜 45° 禁旋转）、C4（深度排序用自定义 Y-Z 轴，非纯 Z）、R4（2.5D 深度排序错误）、`CameraRig.cs`（offset=(0,14,14)，恰 45°）
> 引擎钉定：Unity 2022.3.62 · URP 14.0.12

## 上下文

- 2.5D 斜 45° 微俯视角。**不透明 3D 网格**的遮挡由深度缓冲天然正确，无需干预。
- 排序问题只发生在**透明队列**（Transparent）：飘带/披风面片、法术特效、半透明植被、Billboard、以及未来可能的 2D 精灵挂件。URP 默认按「到相机距离」或纯 Z 排，斜 45° 下**同一地面上前后站位的两个透明体会错排穿插**（R4）。
- 相机永不旋转（C3，`CameraRig` Awake 一次性定死朝向），因此排序轴可以**静态钉死**，不需要每帧跟随相机。

## 备选方案

1. **纯 Z 轴排序**——斜 45° 下 Y（高度）参与视深，纯 Z 错排（技术评估已否，即 C4 的由来）。❌
2. **默认透视距离排序**——对大面片/组合角色以包围盒中心比距离，抖动与穿插不可控。❌
3. **自定义排序轴 `CustomAxis` = 相机前向（Y-Z 合成轴）+ SortingGroup 组合**——Unity 原生支持（`camera.transparencySortMode = TransparencySortMode.CustomAxis`），零每帧成本；组合角色用 `SortingGroup` 整体化，杜绝构件互穿。✅
4. **手写排序组件（每帧改 sortingOrder）**——CPU 成本与维护面大，950M 机器 CPU 也不富裕。❌（保留为个别特例的逃生舱）

## 决定

采用**方案 3**，落地为 `Assets/Scripts/Rendering/DepthSortBootstrap.cs`（挂主相机，`CameraRig` 旁）：

```csharp
// 核心三行（Awake 一次性执行）：
cam.transparencySortMode = TransparencySortMode.CustomAxis;
// 排序轴 = 相机前向：offset=(0,14,14) → 轴取 -offset.normalized ≈ (0, -0.7071, -0.7071)
cam.transparencySortAxis = (-offset).normalized;
```

> **符号推导（S2 实现时修正初稿笔误）**：Unity CustomAxis 语义为「沿轴投影值大者视为更远、先绘制」。
> 相机在 +Y+Z 高处俯视 -Y-Z ⇒ 更远的物体 y+z 更小 ⇒ 轴必须取 (0,-1,-1) 方向（= 相机前向 = -offset.normalized）。
> 初稿示例中的 `(0, 1, 1).normalized` 为符号笔误，若误用会导致前后绘制次序整体反转；以真机 SortingReview 场景肉眼终验（C4）。

- **轴值不写死**：从 `CameraRig.offset` 归一化取反推导（`-offset.normalized`），offset 改 → 轴自动一致；两处单一事实来源。
- **组合体规范**：多面片角色/特效根节点必须挂 `SortingGroup`（写进控制清单）；组内相对顺序用 `sortingOrder` 静态分层。
- **不透明物一律走深度缓冲**：禁止为“排序方便”把不透明材质改成 Transparent（写进控制清单，这是 950M 上 overdraw 的头号杀手）。
- E2-S3（S3 冲刺）再补编辑器 gizmo 可视化，本 Story 不含。

### 验证（CI 可跑部分）

- EditMode/PlayMode 测试：实例化带 `DepthSortBootstrap` 的相机 → 断言 `transparencySortMode == CustomAxis` 且 `transparencySortAxis ≈ (0,-0.7071,-0.7071)`（= -offset.normalized，随 offset 推导）。
- 真机验收：测试场景摆 3 组前后站位透明面片 + 1 个 SortingGroup 组合体，肉眼 + 截图基线确认无穿插错排（C4 Pass）。

## 后果

- ✅ 零每帧 CPU 成本；与禁旋转相机（C3）天然契合；实现量极小（1 脚本 + 1 测试 + 1 规范条目）。
- ✅ 与墨韵栈无交互（Ink Pass 在 AfterRenderingTransparents，排序在其上游已定）。
- ⚠️ 若未来出现「贴地大面片（如水面）与直立面片混排」，CustomAxis 单轴无法两全——届时对贴地面片单独用 `SortingLayer` 或材质队列偏移处理（登记为已知边界）。
- ⚠️ 排序轴只对 Transparent 队列生效——特效师须遵守材质队列规范（控制清单）。
