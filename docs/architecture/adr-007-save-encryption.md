# ADR-007 存档加密与反篡改方案（E0-S5）

> 状态：**提议**（S2 实施前请主理人/制作人确认）
> 关联：ADR-005（存档与数据格式：版本化 JSON + AES + migration，本 ADR 为其加密细化）、C6/P1（不可再生经济反通胀）、R11（存档篡改击穿经济）
> 引擎钉定：Unity 2022.3.62（f3c1，API 等价 f1）· .NET Standard 2.1 / Mono · URP 14.0.12（与本 ADR 无关但随文重申 C1）

## 上下文

- 单机买断 PC 游戏，含**不可再生经济**（P1）：存档若可被随手改数值，稀缺即命运的核心幻想崩塌（R11）。
- 目标是**拦住普通玩家用记事本/十六进制编辑器随手改档**，不是对抗职业逆向（单机游戏密钥必然在客户端，DRM 级防护不现实、不投入）。
- 制作人需要可调试性：开发期能看到明文 JSON。
- 必须可在 CI（无头 Unity，`-batchmode -nographics`）下用 EditMode 测试完整验证（加密/解密/篡改检测/迁移不依赖渲染）。

## 备选方案

1. **明文 JSON**——可调试性最好，但零防护，直接违背 R11/C6。❌
2. **AES-GCM（AEAD，一体化加密+认证）**——算法上最优；但 Unity 2022.3 的 Mono 运行时对 `System.Security.Cryptography.AesGcm` 支持不稳（部分 Mono 构建抛 `PlatformNotSupportedException`），IL2CPP/Mono 双后端行为需逐平台验证，引入不必要的运行时风险。⚠️
3. **AES-256-CBC + HMAC-SHA256（Encrypt-then-MAC）**——两者均为 .NET Standard 2.0 时代 API，Mono/IL2CPP 全平台稳定；Encrypt-then-MAC 是密码学界公认的正确组合顺序；HMAC 即反篡改校验。✅
4. **仅 XOR/Base64 混淆**——伪加密，社区工具秒破，反而给人虚假安全感。❌

## 决定

采用 **方案 3：AES-256-CBC + HMAC-SHA256（Encrypt-then-MAC）**，落地为 `SaveService`（`Assets/Scripts/Services/`）。

### 文件格式（v1）

```
[4B]  MAGIC = "MJFC"
[1B]  containerVersion = 1        （容器格式版本，独立于 saveVersion）
[1B]  flags                       （bit0 = devKey：dev 回退密钥写盘标记，见密钥管理）
[16B] salt                        （每次写盘随机，PBKDF2 用）
[16B] IV                          （每次写盘随机）
[32B] HMAC-SHA256                 （对 MAGIC|version|flags|salt|IV|ciphertext 计算，除 MAC 自身外全文件认证）
[N B] ciphertext                  （AES-256-CBC(JSON UTF-8, PKCS7)）
```

> **S2 实现落定的两处细化**（对初稿的收紧，非方案变更）：
> ① 新增 1 字节 `flags` 承载 dev 位（初稿文字已有 dev 位意图，布局表补齐）；
> ② MAC 覆盖范围从 `salt|IV|ciphertext` 扩大到除 MAC 外的全部字节——任何区段翻 1 字节均被拒读。
> ③ PBKDF2 的 PRF 取 .NET 默认 HMAC-SHA1（3 参构造，Mono/IL2CPP 零兼容风险；本威胁模型下强度足够，初稿未钉定 PRF）。
> ④ dev 密钥档策略按 P3 拍板落地：**警告不拒读**（Debug.LogWarning 后照常读取），发行前收紧另议。

### 密钥管理

- **根密钥**：构建期由 CI 从仓库 Secret（`SAVE_ROOT_SECRET`）注入生成 `Assets/Scripts/Services/Generated/SaveSecret.cs`（gitignore，不进仓库）；本地开发无 Secret 时回退到编辑器专用开发密钥（`#if UNITY_EDITOR` 且写盘文件头标记 dev 位，正式构建拒读 dev 档可后议）。
- **派生**：`PBKDF2(rootSecret, salt, 10_000 iter)` → 64B，前 32B 作 AES key，后 32B 作 HMAC key（加密/认证密钥分离）。
- 迭代次数 10k 在制作人 8GB/老 CPU 机器上 <20ms/次，存档非高频操作，可接受。

### 数据与流程

- **明文层**：版本化 JSON（`saveVersion` 整数 + migration 升级链，来自 ADR-005）。序列化用 Unity 内建 `JsonUtility`（零依赖）；若嵌套字典需求出现再评估 Newtonsoft（Unity 官方 `com.unity.nuget.newtonsoft-json`，不引第三方 DLL）。
- **写盘**：序列化 → 加密 → 写 `save_slotN.tmp` → `File.Replace` 原子替换 → 旧档轮转为 `.bak`（防写盘中断损档）。
- **读盘**：校验 MAGIC/containerVersion → **先验 HMAC**（常数时间比较），失败即拒读并回退 `.bak`；HMAC 通过才解密 → 解析 JSON → 按 `saveVersion` 跑升级链。
- **开发期调试**：编辑器菜单 `MJ → Save → Dump Decrypted JSON`，把当前档解密为明文放 `Temp/`（仅编辑器，正式构建剥离）。

## 后果

- ✅ 普通玩家无法直接编辑存档；任何字节篡改被 HMAC 拒绝（R11 缓解）；写盘中断有 `.bak` 兜底。
- ✅ 全流程纯 C#，EditMode 测试即可覆盖（往返一致/翻转任意字节必拒/migration 链），CI 无头可跑。
- ✅ Mono/IL2CPP 双后端零风险 API。
- ⚠️ 密钥在客户端可被逆向提取——**接受**：目标是防随手改档，不是 DRM。
- ⚠️ 每次 schema 变更须补一个 upgrader 并配套测试（ADR-005 既定成本）。
- ⚠️ CI 需新增 Secret `SAVE_ROOT_SECRET` 与生成步骤（接 E0-S3 流水线，改动 ci.yml 一处）。
