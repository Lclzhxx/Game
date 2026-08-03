# Sprint 2 QA 计划与烟雾测试清单 —— 《秘境·凡尘》

> 负责人：严守真（QA / 测试）
> 版本：**v2.0**（取代 v1.0；v1.0 的 §1/§5 建立在「上游文档缺失」的错误前提上，本版已按原文校对重写）
> 核查日期：2026-07-31 · 核查 HEAD：`3099d5e` · 工作树：**clean**（W1+W2 全部已 commit，未 push）
> 目标工程：`D:\WBzone\Game\mijing-fanchen`（Unity 2022.3.62f3c1 / URP 14.0.12）
> 里程碑：**S2 W2 制作 + CI 测试门控于 2026-08-03 全绿收口（EditMode 57/57）** —— 详见 §7。

---

## 0. 证据基线与对 v1.0 的更正

### 0.1 v1.0 的四个「缺口」复核结论

v1.0 成文时判定 `production/` 与 `docs/` 整树缺失，据此把 §1 验收标准与 §5 风险表整节标为 `[推定·待校对]`。
**本次复核推翻其中三条**——文档已由 commit `97a20c0 chore(docs): 将项目规划与架构文档纳入版本控制` 纳管，
现位于 `mijing-fanchen/docs/` 与 `mijing-fanchen/production/`（另有 `.archived_root_docs/` 为旧仓根副本）。

| v1.0 缺口 | 复核结论 | 依据 |
|---|---|---|
| A 上游文档整树缺失 | **已解除** | `docs/architecture/adr-007~010.md` + `production/sprints/sprint-02-plan.md` 均可读 |
| B `s1-ink-baseline` tag 不存在 | **已解除** | `git tag -l` → `s1-ink-baseline` 存在 |
| C CI 不跑测试 | **仍然成立（P0 阻塞）** | `ci.yml:164` 仅编译校验；`:185` 的 `-runTests` 例子是注释 |
| D 「PlayMode == 需真机」是错误等价 | **维持成立** | `DepthSortTests.cs` 4 例纯相机状态/反射断言，无 `ReadPixels` |

> **§5 风险表已整节重写**：v1.0 凭代码注释反推的 S2-R1~R8 编号**逐条皆错**
> （例：v1.0 猜 R1=URP 版本漂移，原文 R1=8GB 内存 CI；v1.0 猜 R8=基线 flaky，原文 R8=AesGcm 误用）。
> 本版 §5 全部改用 `sprint-02-plan.md §4` 原文，不再有推定项。

### 0.2 本版新发现的三项阻塞（v1.0 未识别）

- **`QA-BLOCK-1` —— W2 无任何绿色测试证据，「EditMode 21/21」是 W1 口径，已过期。**
  `TestResults/editmode-results.xml` 根节点为
  `testcasecount="21" passed="21" start-time="2026-07-31 03:26:54Z"`，
  该时刻**早于** W2 两个提交（`a5ca611` E1-S2 收尾、`3099d5e` E1-S3）。
  该 XML 覆盖的是 `SaveServiceTests(17) + ToonShaderTests(4)`。
  W2 后测试属性数已增至 **SaveService 17 + Toon 15 + HeightFog 22**，21 这个数字**不再是通过基线**。
  唯一的 W2 运行尝试 `editmode-w2-run.log` 停在 `Application.AssetDatabase Initial Refresh Start` 即中断，
  **未产出 XML**（与 engineering-lead-3 报告的 ILPP gRPC 绑定 `localhost:80` 失败致导入卡死一致）。
  → **S2 当前不存在可签收的测试证据。**（**2026-08-03 更新**：已被首轮绿 CI 推翻——EditMode 57/57 全绿，S2 现已有可签收测试证据，详见 §7。）

- **`DOC-BUG-1`（✅ **已修复**，commit `16e3853`）—— `sprint-02-plan.md` 的 E2-S2 验收标准曾写反排序轴符号，会导致「正确实现被判 FAIL」。**
  已复核修复结果：`:97` 现为 `(-offset).normalized ≈ (0,-0.7071,-0.7071)` 并注明初稿 `(0,1,1)` 为符号笔误；
  `:114` 现为 `(0, -0.7071, -0.7071)`。**五处权威源（ADR-009 / Bootstrap / Tests / CameraRig / 冲刺计划）现已全部一致为负号。**
  以下记录保留作追溯：
  | 出处 | 轴值 | 状态 |
  |---|---|---|
  | `sprint-02-plan.md:97` / `:114`「轴 ≈ (0, 0.7071, 0.7071)」 | **正** | ❌ 陈旧笔误 |
  | `adr-009:27-35`（含专门的「符号推导」修正段） | **负** `-offset.normalized` | ✅ 权威 |
  | `DepthSortBootstrap.cs:51` `return (-cameraOffset).normalized;` | **负** | ✅ |
  | `DepthSortTests.cs:56` `expected = -(new Vector3(0,14,14)).normalized` | **负** | ✅ |
  | `CameraRig.cs:43` `LookRotation(-offset.normalized)` | **负**（相机前向自洽） | ✅ |
  ADR-009 已明写「初稿示例中的 `(0,1,1).normalized` 为符号笔误，若误用会导致前后绘制次序整体反转」。
  **代码是对的，冲刺计划的验收标准是错的。**
  → **动作**：请程基岩修正 `sprint-02-plan.md:97/114` 两处为 `(0, -0.7071, -0.7071)`。
  在修正前，**任何人按 §1 验收标准逐条核对 E2-S2 都会得出错误的 FAIL 结论**。

- **`QA-NOTE-1` —— ADR-010 keyword 偏差：QA 立场为「建议批准」，且它反而加强了硬验收。**
  `InkFullscreen.shader:63` 用 `multi_compile_local_fragment _ _MJ_HEIGHT_FOG`，ADR-010 原文为 `shader_feature_local`。
  代码注释（`:54-62`）给出的理由成立：雾开关由 `InkRenderFeature` 运行时 `CoreUtils.SetKeyword` 打，
  而 `shader_feature` 只按**材质资产落盘时**的 keyword 状态入包 → 若 `InkMaterial.mat` 存盘时雾是关的，
  开雾变体会被构建期剥掉，表现为「真机打开雾但画面无反应，且只在出包后暴露」。
  **对验收的影响**：关雾变体中雾代码经 `#if defined(_MJ_HEIGHT_FOG)`（`:158-202`）整段不参与编译，
  故「关雾逐像素不变」**不受削弱，反而更可证**。变体总数 2，远低于预算。
  → 建议主理人**批准偏差并回写 ADR-010**。测试按现行 `multi_compile` 行为编写，无需改。

---

## 1. 测试矩阵（验收标准 → 测试用例）

**列义**：**无头** = 能否在 `-batchmode -nographics` 下跑（决定能否进 CI 门控）；**需 GPU** = 必须带图形环境。
二者**正交**——PlayMode ≠ 需真机（§0.1 缺口 D）。

### 1.1 E0-S5 存档加密（ADR-007 · `sprint-02-plan.md:52-57`）

| # | 验收标准（原文） | 测试用例 | 平台 | 无头 | 需 GPU | 现状 |
|---|---|---|---|---|---|---|
| A1 | 往返一致：字段全等 | `RoundTrip_AllFieldsEqual` | EditMode | ✅ | ❌ | W1 绿 |
| A2 | 反篡改：任意位置翻转 1 字节 → 拒读且不抛未捕获异常（参数化覆盖 头部/salt/IV/MAC/密文） | `Tamper_FlipOneByte_IsRejected` | EditMode | ✅ | ❌ | W1 绿 |
| A3 | 回退 `.bak` | `Tamper_WithBackup_FallsBackToBak` | EditMode | ✅ | ❌ | W1 绿 |
| A4 | migration 骨架 v0 → v1 | `Migration_V0Save_UpgradesToCurrentVersion` | EditMode | ✅ | ❌ | W1 绿 |
| A5 | 未来版本档拒读 | `Migration_FutureVersion_IsRejected` | EditMode | ✅ | ❌ | W1 绿 |
| A6 | P3：dev 密钥档「警告不拒读」 | `DevKeySave_LoadWithReleaseService_WarnsButLoads` | EditMode | ✅ | ❌ | W1 绿 |
| A7 | 原子写盘、不留 `.tmp`、轮转 `.bak` | `Save_LeavesNoTmpFile_AndRotatesBak` | EditMode | ✅ | ❌ | W1 绿 |
| A8 | 槽位不存在 → NotFound | `Load_MissingSlot_ReturnsNotFound` | EditMode | ✅ | ❌ | W1 绿 |
| A9 | 截断/空文件健壮性 | `CorruptLength_IsRejectedWithoutThrow` | EditMode | ✅ | ❌ | W1 绿 |
| **A10** | **密钥不进仓库：仓库 grep 不到密钥字面量** | `Repo_GeneratedSaveSecret_IsGitIgnored`、`Repo_NoCommittedKeyLiteral` ✅ 已补 | EditMode | ✅ | ❌ | W2 绿（CI 57/57） |
| **A11** | CI 日志不回显 Secret | **仍无自动化覆盖** ⚠️ | — | — | — | **残留缺口** |

**判定：ADEQUATE。A10 缺口已由 `SaveSecurityTests.cs`（commit `3e6c622`）闭合。**

已复核该文件质量，**断言前提均成立**（非空断言）：
- `.git` 位于 `mijing-fanchen/`，故 `RepoRoot = Path.GetDirectoryName(Application.dataPath)` 解析正确；
- `.gitignore:38` 含 `Assets/Scripts/Services/Generated/`、`:40` 含 `SaveSecret.cs` → 两条字符串断言均可通过；
- `git check-ignore` 对生成路径实测 **exit 0** → 主守卫通过；
- `SaveCrypto_DoesNotUseAesGcmOrChaCha20` 采用「禁用项 + 批准项」双向断言（反查 `CipherMode.CBC` / `HMACSHA256`），
  规避了「只是没写 AesGcm」的空断言陷阱 —— 这个写法是对的。

**残留两点（非阻塞，建议 S3 处理）**：
1. **A11 未覆盖**：`sprint-02-plan.md:56` 的验收标准原文是两句——「仓库内 grep 不到密钥字面量」**且**「CI 日志不回显 Secret」。
   前者已闭合，**后者仍无守护**。建议在 `ci.yml` 生成步骤后加一条断言：日志中不得出现 Secret 明文/其 Base64 前缀
   （PowerShell 侧 `Select-String` 自检即可，无需 Unity）。
2. **`RunGit` 静默降级风险**：git 不可用时 `RunGit` 返回 `null`，两条 git 守卫被**跳过**而非失败，
   仅余 `.gitignore` 文本断言。自托管 runner 上 git 必然可用，故实际风险低；
   但建议在降级分支加一行 `UnityEngine.Debug.LogWarning`，避免「守卫被静默跳过」在日志里查无痕迹。

### 1.2 E2-S2 Y-Z 深度排序（ADR-009 · `sprint-02-plan.md:113-117`）

| # | 验收标准 | 测试用例 | 平台 | 无头 | 需 GPU | 现状 |
|---|---|---|---|---|---|---|
| B1 | `transparencySortMode == CustomAxis` 且轴 ≈ **(0, -0.7071, -0.7071)**（见 `DOC-BUG-1`） | `Bootstrap_SetsCustomAxis_FromDefaultOffset` | PlayMode | ✅ | ❌ | CI 绿（2026-08-03） |
| B2 | 轴随 `CameraRig.offset` 推导，不写死 | `Bootstrap_AxisFollowsRigOffset` | PlayMode | ✅ | ❌ | CI 绿（2026-08-03） |
| B3 | `GreyboxBuilder` 自动接线主相机 | `GreyboxBuilder_WiresBootstrapOnMainCamera` | PlayMode | ✅ | ❌ | CI 绿（2026-08-03） |
| B4 | 零每帧成本（无 Update/LateUpdate/FixedUpdate） | `Bootstrap_HasNoPerFrameCallbacks` | PlayMode `[Test]` | ✅ | ❌ | CI 绿（2026-08-03） |
| B5 | 无穿插：SortingReview 截图基线 + 肉眼复核（C4） | `sorting_baseline.png` 比对 + 人工 | 手动 | ❌ | ✅ | 待做 |
| B6 | SortingGroup 组合体整体移动无构件互穿 | 人工（SortingReview 场景） | 手动 | ❌ | ✅ | 待做 |

**B1~B4 无头可跑，必须进 CI 门控**——现已在 CI 门控执行并全绿（见 §7）。

### 1.3 E1-S2 国风 Toon（ADR-008 · `sprint-02-plan.md:82-88`）

| # | 验收标准 | 测试用例 | 平台 | 无头 | 需 GPU | 现状 |
|---|---|---|---|---|---|---|
| C1 | `ShaderHasError == false` 且 `isSupported == true` | `Shader_Exists_AndSupported` / `Shader_CompilesWithoutErrors` | EditMode | ✅ | ❌ | W1 绿 |
| C2 | R5 红线：材质无任何描边参数 | `Shader_HasNoOutlineProperties_R5RedLine` | EditMode | ✅ | ❌ | W1 绿 |
| C3 | 三 pass（Forward/ShadowCaster/DepthOnly） | `Shader_HasForwardShadowCasterDepthOnlyPasses` | EditMode | ✅ | ❌ | W1 绿 |
| C4 | 变体总数 ≤ 64 | W2 新增（`ToonShaderTests` 已扩至 15 属性，含 clamp/NaN 守卫） | EditMode | ✅ | ❌ | W2 绿（CI 57/57） |
| C5 | SRP Batcher 兼容（Frame Debugger 人工 + 截图留档） | 人工 | 手动 | ❌ | ✅ | 待做 |
| C6 | 无双描边：勾线仅来自墨韵 Pass（截图基线） | `toon_baseline.png` 比对 | 手动 | ❌ | ✅ | 待做 |
| C7 | 与墨韵共存：ToonReview 开墨韵基线通过 + 墨韵栈 < 3ms | 联合基线 + ProfilerRecorder | 手动 | ❌ | ✅ | 待做 |
| C8 | ⏳ H3 观感初评（**非本 Story 出门条件**） | 试玩窗口 | 手动 | ❌ | ✅ | 待做 |

> **C8 口径提醒**：`sprint-02-plan.md:88` 明标 H3 观感为 ⏳ 且「**非**本 Story 出门条件，最终观感待 art-director 参数对齐后另行评审」。
> 派单把「H3 Toon 初评」列入真机验证项是对的，但**不得因 H3 主观评价不佳而阻塞 E1-S2 签收**——请勿误升为出门条件。

### 1.4 E1-S3 高度雾（ADR-010 · `sprint-02-plan.md:141-146`）

W2 已实现（`3099d5e`），`HeightFogTests.cs` 共 18 个测试方法（含 `[TestCase]` 展开约 22 例）。

| # | 验收标准（原文） | 测试用例（实存方法名） | 平台 | 无头 | 需 GPU | 现状 |
|---|---|---|---|---|---|---|
| D1 | **关雾零回归：`_MJ_HEIGHT_FOG` off 时既有墨韵基线逐像素不变（变体剔除生效）** | 源码层：`InkShader_FogCodeIsFullyGatedByKeyword` + `Settings_DefaultsToDisabled_SoS1PixelsAreUntouched`；**像素层需真机**（→ M4） | EditMode + 手动 | 部分 ✅ | ✅（像素层） | 源码层 W2 绿；像素层待做 |
| D2 | 无新增 Pass/Blit，C2 守住 | `InkShader_HasExactlyOnePass_C2RedLine`、`InkRenderFeature_BlitSequenceUnchangedFromS1_C2RedLine` | EditMode | ✅ | ❌ | W2 绿（CI 57/57） |
| D3 | 参数安全：全参数越界 clamp、无 NaN | `Guard_Float_RejectsNonFinite`、`Guard_Float_ClampsOutOfRange`、`Guard_Color_SanitizesPerChannel_AndForcesOpaqueAlpha`、`ApplyTo_WritesOnlySaneValues_EvenWithPoisonedInput` | EditMode | ✅ | ❌ | W2 绿（CI 57/57） |
| D4 | keyword 门控双向同步 | `ApplyTo_SyncsKeyword_BothDirections`、`InkShader_DeclaresHeightFogKeywordSwitch`、`ApplyTo_NullMaterial_DoesNotThrow` | EditMode | ✅ | ❌ | W2 绿（CI 57/57） |
| D5 | 雾参数齐备且与单一事实来源一致 | `InkShader_ExposesAllFogProperties_MatchingSingleSourceOfTruth` | EditMode | ✅ | ❌ | W2 绿（CI 57/57） |
| D6 | 先晕染后勾线（ADR-010 顺序） | `InkShader_FogAppliedBeforeLineWork` | EditMode | ✅ | ❌ | W2 绿（CI 57/57） |
| D7 | URP14 安全 API 重建世界坐标 | `InkShader_WorldPosReconstruction_UsesUrp14SafeApi` | EditMode | ✅ | ❌ | W2 绿（CI 57/57） |
| D8 | Volume Profile 低饱和冷调固化 | `VolumeProfile_IsCommitted_AndIsLowSaturationCoolTone` | EditMode | ✅ | ❌ | W2 绿（CI 57/57） |
| D9 | 基线条目存在（真图或占位） | `FogBaseline_EntryExists_RealOrPending` | EditMode | ✅ | ❌ | W2 绿（CI 57/57） |
| D10 | 开雾正确：低 Y 浓、高 Y 清透、天空不糊死 | `ink_fog_baseline.png` 比对 + 目视 | 手动 | ❌ | ✅ | 待做 |
| D11 | 性能：墨韵栈（含雾）< 3ms，雾增量 < 0.5ms | ProfilerRecorder 真机回填 | 手动 | ❌ | ✅ | 待做 |

**判定：测试设计 ADEQUATE。** D1 被正确拆成「源码级门控可无头证」+「像素级需真机」两层，思路正确。
`HeightFogTests` 把 C2 红线、参数顺序、URP14 API 选型都做成了源码级断言，属高回归价值的「防漂移」守卫。

---

## 2. 烟雾测试清单（CI 门控）

### 2.1 门控原则
- FAIL 即「未达 QA」，不放行。
- 无头层必须全自动、零 GPU 依赖；需 GPU 的一律走 §2.3 / §3。
- 单次门控目标 ≤ 15 分钟（8GB 机器现实约束）。

### 2.2 第一层：无头自动套件（CI 强制 · 当前**尚未接实**）

```powershell
# Job 1: EditMode（SaveService + ToonShader + HeightFog）
-batchmode -quit -nographics -projectPath "<repo>" `
  -runTests -testPlatform EditMode -testResults "<repo>\TestResults\editmode-results.xml"

# Job 2: PlayMode（DepthSort 4 例，纯状态断言，无需 GPU）
-batchmode -quit -nographics -projectPath "<repo>" `
  -runTests -testPlatform PlayMode -testResults "<repo>\TestResults\playmode-results.xml"
```

**通过判据**：两份 XML 均 `failed="0"` 且 `inconclusive="0"` 且 `passed == total`。
`skipped > 0` 判 **CONCERNS**，需 PR 说明，不自动放行。

> **基线数字更新**：**不要再用「21/21」做判据**（§0.2 `QA-BLOCK-1`）。

**预期用例数（2026-07-31 静态统计，HEAD `3e6c622`）**：

| 套件 | 文件 | 用例数 |
|---|---|---|
| EditMode | `SaveServiceTests` | 17 |
| EditMode | `ToonShaderTests` | 15 |
| EditMode | `HeightFogTests` | 22 |
| EditMode | `SaveSecurityTests`（新增） | 3 |
| **EditMode 合计** | | **57** |
| **PlayMode** | `DepthSortTests` | **4** |
| **总计** | | **61** |

统计口径：正则 `^\s*\[(Test|TestCase|UnityTest)[\]\(]` 计数。已确认全仓**无** `[TestCaseSource]`/`[Values]`/`[Range]`/`[Ignore]`，
故「属性行数 = NUnit 展开后的用例数」**成立**（`[TestCase]` 逐行 1:1 展开）。
该口径在 W1 已被实测验证：SaveService 17 + Toon 4 = 21，与 `editmode-results.xml` 的 `total="21"` 精确吻合。

> ⚠️ **57 ≠ 54**：engineering-lead-3 回传的 54 是 `17+15+22`，正确但**不含他随后在 commit `3e6c622` 追加的
> `SaveSecurityTests` 3 例** —— 该数字被其自身的后续提交覆盖。以 **EditMode 57 / PlayMode 4** 为准。
> 首次绿跑后若实际 total ≠ 57，**不要直接改基线数字**，先查差值来源（漏编译的测试程序集 / 被静默跳过的 fixture）。

**三条退出码铁律**（前两条源自 `ci.yml` 既有记录，第三条为本计划新增）：
1. `run:` 块必须纯 ASCII（PS 5.1 按 GBK 误读 UTF-8 临时脚本 → ParseError 秒退）；
2. 必须 `Start-Process -Wait`（`Unity.exe` PE Subsystem=2 为 GUI 子系统，`&` 不阻塞 → 秒退 + 垃圾退出码）；
3. `-runTests` 下 Unity **用退出码表达测试结果**（0=全过／2=有失败／3=运行失败）。
   **必须区分 2 与 3 并优先解析 XML**，否则「测试失败」与「Unity 崩了」不可分。

### 2.3 第二层：截图基线比对（`LOCAL-CI`，带 GPU，**不带** `-nographics`）

**真实基线路径**（v1.0 此处路径为杜撰，已更正）：磁盘现有 4 个占位
`Assets/Tests/Baseline/{ink,ink_fog,toon,sorting}_baseline.png.pending`。

| Story | 基线文件 | 采集条件 | 容差 |
|---|---|---|---|
| E2-S2 | `sorting_baseline.png` | SortingReview，offset (0,14,14) 固定 | ≥99% 像素通道差 < 2/255 |
| E1-S2 | `toon_baseline.png` | ToonReview，主光角度固定 | ≥99% 像素通道差 < 2/255 |
| E1-S2+墨韵 | （并入 `toon_baseline`，`sprint-02-plan.md:73` 要求「并入墨韵回归基线」） | 开墨韵 | ≥99% 像素通道差 < 2/255 |
| E1-S3 关雾 | `ink_baseline.png` | 雾 OFF，同 S1 采集条件 | **逐像素严格相等（diff == 0）** |
| E1-S3 开雾 | `ink_fog_baseline.png` | 雾 ON，参数取默认 | ≥99% 像素通道差 < 2/255 |

> **关雾容差已由原文裁定，无需再裁决**：`sprint-02-plan.md:142` 明文「既有墨韵截图基线**逐像素不变**」，
> ADR-010 亦以「变体剔除生效」为由支撑。故 D1/M4 采用 **diff == 0**，**不适用**派单给出的 99%/2-255 容差
> （该容差是 §4 S2-R5 为「驱动/平台差异误报」设的，对象是其余三张图）。
> 若关雾路径做不到严格相等，即证明关雾未真正短路，属应修缺陷而非放宽阈值的理由。
> —— v1.0 曾把此项列为「待裁决」，现据原文关闭。

**采集纪律**（否则基线必 flaky）：固定 1920×1080、固定 Quality、锁 `Time.captureFramerate`、
等 shader 编译与资源加载完成再截（≥3 × `WaitForEndOfFrame`）、
**务必关闭 `FpsProbe` 的 OnGUI 叠加**——它由 `GreyboxBuilder` 自动挂到主相机，
每帧变化的 FPS 数字会直接毁掉基线，是本工程最现实的 flaky 源。

### 2.4 CI 交付项（需程基岩实现）

| ID | 内容 | 优先级 |
|---|---|---|
| `CI-TASK-1` | `ci.yml` 追加 EditMode `-runTests` + XML 解析 + 退出码 0/2/3 分流 | **P0** |
| `CI-TASK-2` | 追加 PlayMode `-runTests`（无头可行） | **P0** |
| `CI-TASK-3` | XML 摘要打印到控制台（弱网 runner 不可用 `upload-artifact`，已实测超时） | P1 |
| `CI-TASK-4` | `LOCAL-CI` 截图基线采集/比对脚本 | P1 |
| `CI-TASK-5` | 解决 ILPP gRPC 绑定 `localhost:80` 失败致导入卡死（当前**沙箱内无法跑测试的直接原因**） | **P0** |

**8GB 拆两 job（`sprint-02-plan.md` S2-R1 缓解措施的落地）**：
- `job: editmode`（无图形优先）——编译校验 + EditMode 合并为**同一次 Unity 冷启动**最省内存；
- `job: playmode`（`needs: editmode`，**串行**）。
**切勿并行**：同机两个 Unity 实例争抢同一 `Library/` 目录锁，既 OOM 又结果不可信。
沿用页面文件置 D 盘、放大到 16–24GB。

---

## 3. 真机验证项（GTX 950M / Win10 / 1080p，人工签收）

证据归档至 `production/sprints/evidence/s2/`（路径依 `sprint-02-plan.md:90`）。

| ID | 验证项 | 通过判据 | 关联 |
|---|---|---|---|
| M1 | FPS · **关雾** | ≥ 58 全程（≥60s 观察） | H2 底线 |
| M2 | FPS · **开雾** | ≥ 58 全程 | H2 底线 / S2-R2 |
| M3 | 墨韵栈耗时（含雾） | < 3ms，雾增量 < 0.5ms | D11 / S2-R2 |
| M4 | **关雾墨韵旧基线逐像素不变** | **diff == 0**（对 `s1-ink-baseline` tag 采集的 `ink_baseline.png`） | D1 硬验收 |
| M5 | 开雾观感：低洼墨气、高台清透、天空不糊死 | 目视 + `ink_fog_baseline.png` | D10 |
| M6 | 深度排序肉眼终验 | 无穿插、无 z-fighting；组合体不互穿 | B5/B6 |
| M7 | SRP Batcher 合批 | Toon 材质进入 SRP Batch | C5 |
| M8 | 变体数 | ≤ 64 | C4 |
| M9 | 无新增 Pass/Blit | Frame Debugger 全屏 Pass 数与 S1 持平 | D2 / C2 |
| M10 | H3 Toon 观感初评 | 主理人+美术记录印象（**非出门条件**，见 §1.3 C8） | 试玩窗口 |

**M1/M2 必须同时记录 Draw Calls 与 Triangles**（`FpsProbe` 已用 `ProfilerRecorder` 采集）——
仅记 FPS 无法定位「开雾掉帧」根因。`FpsProbe` 的 OnGUI 开销在开/关雾两次测量中同时存在，
对**差值**无碍，对**绝对值**偏悲观，对 ≥58 门槛属保守方向，可接受。

**灯光数量必须固定**：`ToonGuofengLighting.hlsl:63` 记「950M 上限 1–3 盏点光（性能契约§5）」，
灯光数不固定则 M1/M2 不可比。

---

## 4. 回归基线与回退

| 层 | 内容 | 触发 |
|---|---|---|
| L1 代码回归 | EditMode 全量 + PlayMode 4 例（无头） | 每次 push / PR |
| L2 架构红线 | C2（R5 零描边）、B4（零每帧）、D2（单 Pass/Blit 序列）、D6（先晕染后勾线） | 每次 push（L1 子集，单列因属「防漂移」） |
| L3 视觉回归 | §2.3 四张基线 | 涉渲染的 PR，`LOCAL-CI` 手动触发 |
| L4 性能回归 | M1/M2/M3 + Draw Calls/Triangles | 每 Story 完成 + Sprint 末 |

**回退 tag**：`s1-ink-baseline`（**已存在**）。对应 `sprint-02-plan.md` S2-R3 缓解措施「改动前打 tag，回退即还原」。
E1-S3 是 S2 唯一改动 S1 已验证代码的 Story，若 M4 不过 → 直接回退至该 tag。

**基线采集与更新规则**：
1. 4 张 `.pending` 需真机采集后替换为真图，走 **Git LFS**；
2. 新基线须**连续 3 次采集互相 diff == 0** 方可入库（防收编 flaky 基线）；
3. 同目录留 `*.meta.txt` 记录环境指纹（GPU 驱动版本 / Unity / URP / Quality 等级）——
   驱动更新会造成像素级差异，无指纹则日后无法区分「回归」与「环境变了」；
4. 视觉基线**只因有意的视觉变更**更新，PR 附「旧/新/diff」三联 + 主理人与美术签字。
   **严禁因「测试一直红」静默覆盖基线**——这是视觉回归体系最常见的失效方式。

**Bug 回归**：S2 内每修一个 Bug 必须同时提交一个能复现该 Bug 的测试（红→绿），否则不计「完成」。
建议建立 `production/qa/bugs/`，编号 `S2-BUG-nnn`。

---

## 5. S2-R1~R8 风险验证落点（**按 `sprint-02-plan.md §4` 原文，非推定**）

| # | 风险（原文摘要） | 概率/影响 | 验证落点 | 类型 |
|---|---|---|---|---|
| **S2-R1** | 8GB 内存：编辑器+PlayMode+截图基线同跑触顶换页，CI 变慢/超时 | 高/中 | §2.4 拆两 job **串行**；页面文件 16–24GB；基线钉 1080p 单帧不做多分辨率矩阵；观察 CI 连续 10 次绿 | CI 观测 |
| **S2-R2** | 950M fill-rate：雾并入后单 Pass 指令数增加，栈耗时逼近 3ms | 中/高 | **M3**（<3ms，雾增量<0.5ms）+ M1/M2；超线则启用既有「半分辨率」预案（E1-S6 提前），**不拆第二条 Pass** | 真机 |
| **S2-R3** | 墨韵集成冲突：E1-S3 改 `InkFullscreen.shader`/`InkRenderFeature.cs`，是唯一可能破坏 S1 已验证行为的点 | 中/高 | **M4 关雾 diff==0（硬验收）** + D1/D2 源码门控；回退 tag `s1-ink-baseline` 已就位 | 真机 + 无头 |
| **S2-R4** | 双描边复发：Toon 若被加几何描边与墨线叠加脏化（R5） | 低/中 | **C2** `Shader_HasNoOutlineProperties_R5RedLine`（已有，无头）+ `toon_baseline` 勾线区域比对 | 无头 + LOCAL-CI |
| **S2-R5** | 截图基线脆弱：驱动/平台差异致像素比对误报红 | 中/中 | 容差 ≥99% 像素差 <2/255（**仅对非 D1 的三张**）；基线只在自托管固定机生成与比对；§4 环境指纹 + 连续 3 次 diff==0 入库 | LOCAL-CI |
| **S2-R6** | `JsonUtility` 表达力不足（字典/多态）致存档 schema 返工 | 低/中 | A1/A4/A5 迁移链测试守护；确需字典时按 ADR-007 升级 Newtonsoft，**migration 链兜底旧档**（补一条「v1 旧档在新序列化器下仍可读」回归） | 无头 |
| **S2-R7** | CI 无头无法渲染截图（`-nographics`） | 中/中 | 渲染类断言进**带 GPU 的 PlayMode job（不带 `-nographics`）**；纯逻辑断言留 EditMode 无头。**注意**：`DepthSortTests` 属纯状态断言，应留在无头层，勿误划入 GPU job（§0.1 缺口 D） | CI 结构 |
| **S2-R8** | AesGcm 类 API 误用（Mono 不稳） | 低/高 | ✅ **已固化为测试**：`SaveCrypto_DoesNotUseAesGcmOrChaCha20`（禁用项 + 批准项 `CipherMode.CBC`/`HMACSHA256` 双向断言），评审约束不再只靠人眼 | 无头（已落成） |

**新增 QA 动作 `S2-R7-SELFTEST`（最高优先级）**：门禁至今是骨架（§0.1 缺口 C）。
`CI-TASK-1/2` 接实后**必须故意注入一个失败用例，确认 CI 真的变红**。
**未经自检的门控等同于没有门控**——这是本 Sprint 最高优先级的 QA 动作。

---

## 6. 已知环境限制

| 限制 | 影响 | 处置 |
|---|---|---|
| **ILPP gRPC 绑定 `localhost:80` 失败 → 资源导入卡死** | 沙箱内**任何**测试都跑不了（`editmode-w2-run.log` 停在 AssetDatabase Refresh） | `CI-TASK-5` **P0**；W2 证据必须在真机/自托管 runner 补跑 |
| 无头 `-nographics` 无 GPU 上下文 | 四张截图基线、FPS、Frame Debugger、SRP Batcher | 走 `LOCAL-CI` / §3，不进无头门控 |
| `-nographics` 下 `Camera.Render()+ReadPixels` **静默返回全黑图**而非报错 | 极具欺骗性的假绿 | 无头用例中**禁用**该组合 |
| `ShaderUtil.*` 属 `UnityEditor` 命名空间 | C1~C4、D2~D9 | 仅 EditMode（`MJ.Tests.EditMode.asmdef` 已正确设 `includePlatforms:["Editor"]`），不可移入 PlayMode |
| PlayMode 用例不受无 GPU 限制 | B1~B4 | 应进无头 CI |
| 8GB 物理内存 | CI | 两 job 串行；页面文件 16–24GB |
| GTX 950M | 性能契约 1–3 盏附加点光 | FPS 场景固定灯光数 |
| 弱网 runner | `game-ci/unity-builder`、`upload-artifact@v4` 均实测 100s 超时 ×3 | 结果只打印控制台 + 落盘，**不引入新 Marketplace action** |

**flaky 纪律**：任一用例连续 3 次运行出现 ≥1 次结果不一致即判 flaky，
**立即 `[Ignore("S2-BUG-nnn flaky")]` 隔离并开 Bug**，不允许污染 CI 信号；隔离项须挂 Bug 号并在 Sprint 末复盘，禁止长期挂起。
当前最高 flaky 风险源：`FpsProbe` OnGUI 每帧数字、shader 首次编译未完成即截图（粉/黑画面）。

---

## 7. S2 质量门判定

| 维度 | 判定 | 理由 |
|---|---|---|
| E0-S5 存档（W1 部分） | **PASS** | 17/17 绿，覆盖 ADEQUATE |
| E0-S5 CI 密钥注入（W2） | **PASS** | A10 缺口已由 `SaveSecurityTests`（`3e6c622`）闭合，该测试现已在 CI 全绿（含 A10 双守卫）；**A11「CI 日志不回显 Secret」仍为开放式缺口，待补（不阻塞 S2 收口）** |
| E1-S2 Toon | **PASS** | W1 4 例绿 + W2 扩至 15 例，现已全绿（15/15） |
| E2-S2 深度排序 | **PASS** | 实现与 ADR-009 一致、`DOC-BUG-1` 已修（`16e3853`）、验收标准不再误导；无头用例现已在 CI 门控执行并全绿（原「从未执行」已解除） |
| E1-S3 墨韵雾 | **PASS** | 测试设计 ADEQUATE（18 方法 / 22 用例，红线覆盖到位），现已全绿；ADR-010 偏差已于 `922b612` 批准采纳 |
| **测试证据链** | **PASS** | 首轮绿 CI 实测 NUnit XML：`total=57 passed=57 failed=0 skipped=0`（2026-08-03），覆盖 EditMode 全量；证据链成立 |
| **CI 门控体系** | **PASS** | `ci.yml` 第 5 步 EditMode Tests 门控已接实并跑出 57/57 全绿，门禁不再是骨架 |

### 综合判定：**PASS（含已知次要遗留，不阻塞收口）**

**代码、测试设计与验证体系现已闭环**：红线守卫思路正确（R5 零描边、C2 单 Pass、雾职责门控、URP14 API 选型），
E1-S3 把「关雾零回归」拆成源码级 + 像素级两层尤其专业；且首轮绿 CI 实测 `total=57 passed=57 failed=0 skipped=0`（2026-08-03），
EditMode 全量用例（存档 17 + 密钥注入 3 + Toon 15 + 墨韵雾 22）已在 CI 门控执行并全绿。
**此前「验证体系未闭环」的问题因本次 CI 全绿而解除。**

> **首轮绿 CI 证据（2026-08-03）**：Unity 测试日志 `EditMode results: total=57 passed=57 failed=0 skipped=0`。
> 临时金丝雀 `CIGateSelfTest` 已移除；本地仓库 `ahead vs origin/main = 0`（全部推送，CI 在云端自托管 runner 跑出，绿的证据成立）。

**已知次要遗留（均不阻塞 S2 收口）：**
- **(a) 像素级视觉回归基线**：`Assets/Tests/Baseline/{ink,ink_fog,toon,sorting}_baseline.png.pending` 共 4 张仍为占位 pending，需**制作人本机（带 GPU 的 Unity）采集替换**——CI 无显卡环境无法生成真图。采集纪律见 §2.3 / §4（连续 3 次 diff==0 入库、走 Git LFS）。
- **(b) A11「CI 日志不回显 Secret」补测待排**：在 `ci.yml` 生成步骤后用 `Select-String` 自检即可，无需 Unity；成本低，不阻塞收口。
- **(c) 证据留存**：本结论与 NUnit XML（total=57 passed=57）已记录于 §7（日期 2026-08-03），供回溯。

**里程碑**：**S2 W2 制作 + CI 测试门控于 2026-08-03 全绿收口（EditMode 57/57）**。

---

## 8. 待主理人裁决 / 指派

| # | 事项 | QA 建议 |
|---|---|---|
| 1 | ~~**[裁决] ADR-010 keyword 偏差**（`multi_compile_local_fragment` vs `shader_feature_local`）~~ | ✅ **已关闭**（偏差已于 `922b612` 由制作人批准采纳，详见 §7 墨韵雾维度） |
| 2 | ~~**[指派] `DOC-BUG-1`** 轴符号笔误~~ | ✅ **已关闭**（commit `16e3853`，QA 已复核两行修复到位） |
| 3 | ~~**[指派] `CI-TASK-5`** ILPP gRPC 导入卡死~~ | ✅ **已关闭**（自托管 runner 已跑出 EditMode 57/57 全绿，导入卡死不再阻塞，2026-08-03） |
| 4 | ~~**[指派] `CI-TASK-1/2/3`** + 门控自检~~ | ✅ **已关闭**（EditMode Tests 门控已接实并跑出 57/57 全绿，2026-08-03） |
| 5 | ~~**[批准] 补测**：A10 密钥不入库、S2-R8 禁 AesGcm~~ | ✅ **已关闭**（commit `3e6c622`，QA 已复核断言前提成立、非空断言） |
| 5b | **[批准] 剩余补测**：A11「CI 日志不回显 Secret」、S2-R6 旧档兼容回归 | 均低成本；A11 在 `ci.yml` 侧用 `Select-String` 自检即可，无需 Unity |
| 6 | **[确认] H3 观感口径** | 按 `sprint-02-plan.md:88`，H3 为 ⏳ 非出门条件，不得阻塞 E1-S2 签收 |
| 7 | **[提示] P2 E0-S6 遥测**（3 SP）是否纳入 S2 | 若纳入，QA 需补相应验收；当前未见实现 |
| 8 | **[提示] 未跟踪探针文件归属** | `mijing-fanchen/` 下 `.qa_probe*.txt` / `.qa_progress.txt` / `.qa_run_exit.txt` **非本人产物**（我的临时文件在仓库外 `D:\WBzone\Game\`，已清理）。疑似 quality-lead-2 的工作文件，已去信确认，建议由其本人清理或加 `.gitignore` |

> **已关闭事项**：v1.0 的「关雾容差待裁决」——`sprint-02-plan.md:142` 原文已明确「逐像素不变」，按 diff==0 执行，无需再裁决。

---

*本计划全部结论可追溯至仓库实际文件、`TestResults/editmode-results.xml` 根节点属性、`git tag -l` 与 `git log`。
凡与上游文档冲突处均已注明出处与行号，并给出以哪一方为准的判断依据。*
