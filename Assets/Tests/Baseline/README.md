# 截图基线（Screenshot Baselines）

> 归属：S2 · E1-S2（Toon）/ E1-S3（高度雾）/ E2-S2（Y-Z 排序）
> 相关：`production/sprints/sprint-02-plan.md` §1、风险 S2-R5 / S2-R7、ADR-008 / ADR-010

## 当前状态

| 基线文件 | 来源场景 | 采集条件 | 状态 |
|---|---|---|---|
| `toon_baseline.png` | `Assets/Tests/Scenes/ToonReview.unity` | 墨韵 Feature 开、`_MJ_HEIGHT_FOG` **关** | ⏳ 待真机采集 |
| `ink_baseline.png` | `Assets/Tests/Scenes/ToonReview.unity` | 墨韵 Feature 开、`_MJ_HEIGHT_FOG` **关** | ⏳ 待真机采集 |
| `ink_fog_baseline.png` | `Assets/Tests/Scenes/ToonReview.unity` | 墨韵 Feature 开、`_MJ_HEIGHT_FOG` **开** | ⏳ 待真机采集 |
| `sorting_baseline.png` | `Assets/Tests/Scenes/SortingReview.unity` | 墨韵 Feature 开 | ⏳ 待真机采集 |

目录内的 `*.png.pending` 是**占位标记**，不是图片。刻意不放假 PNG——假图会被比对工具当成真基线通过，
制造「绿了但没验」的假象。采集到真图后请删掉对应的 `.pending` 标记。

## 为什么现在没有图

沙箱/无头环境 `SystemInfo.graphicsDeviceType == Null`（`-nographics`），根本渲染不出画面（S2-R7）。
基线**只在固定机器**上采集与比对——制作人本机 Unity Hub，或带 GPU 的自托管 runner；
换机器采基线会引入驱动差异导致误报红（S2-R5）。

## 采集步骤（制作人本机，5 分钟）

1. Unity Hub 打开工程，`git lfs pull` 确保 LFS 资产完整。
2. 打开 `Assets/Tests/Scenes/ToonReview.unity`。
3. 菜单 `MJ → Test → Build Toon Review Scene`（编辑器内直接搭，不用进 Play）。
4. 确认 URP Renderer 资产上的 `InkRenderFeature`：
   - `inkMaterial` 已指到 `Assets/Materials/InkMaterial.mat`；
   - `Height Fog → enabled` = **false**。
5. 菜单 `MJ → Test → Capture Baseline - toon_baseline` 与 `... - ink_baseline`。
6. 把 `Height Fog → enabled` 改成 **true**，再 `MJ → Test → Capture Baseline - ink_fog_baseline`。
7. 打开 `Assets/Tests/Scenes/SortingReview.unity`，进 Play 一帧后菜单 `MJ → Test → Capture Baseline - sorting_baseline`
   （排序场景靠 `Start()` 搭场景，需要 Play；仍留在 Play 模式时截）。
8. 删除对应的 `*.png.pending` 标记文件，提交（`*.png` 已在 `.gitattributes` 走 LFS，提交前用
   `git lfs status` 确认它是 LFS 对象而不是普通二进制）。

## 回归比对

菜单 `MJ → Test → Compare Active Scene Against Baseline...`，两档严格度：

- **逐像素严格**（100% 像素、0 差异）——**E1-S3 硬验收专用**：
  `_MJ_HEIGHT_FOG` 关闭时，当前画面必须与 `ink_baseline.png` / `toon_baseline.png` 逐像素一致，
  证明高度雾在关闭态下对 S1 已验证墨韵行为**零影响**。
- **容差**（≥99% 像素、单通道差 < 2/255）——常规观感回归，吸收驱动噪声（S2-R5）。

## 分辨率

钉死 **1920x1080 单档**，不做多分辨率矩阵（8GB 机器内存预算，S2-R1）。
改分辨率 = 全部基线作废重采，请勿随手改 `BaselineCaptureMenu.CaptureWidth/Height`。
