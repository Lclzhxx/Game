// =============================================================
// 文件：SaveData.cs（E0-S5，ADR-007 / ADR-005）
// 作用：存档明文 schema v1。版本化 JSON 的数据载体，JsonUtility 序列化。
// 规范（控制清单）：
//   - schema 刻意扁平（S2-R6：JsonUtility 不支持字典/多态，需求出现前不引 Newtonsoft）。
//   - 【任何字段变更必须 saveVersion+1，并在 SaveMigrations 补 upgrader + 测试】。
//   - 本类只是数据袋：零逻辑、零 UnityEngine 生命周期，纯 [Serializable]。
// 注意：Vector3 是 JsonUtility 原生支持类型，安全。
// =============================================================

using System;
using UnityEngine;

namespace MJ.Services
{
    [Serializable]
    public class SaveData
    {
        /// <summary>当前 schema 版本。字段变更必须 +1 并补 upgrader（控制清单第 5 条）。</summary>
        public const int CurrentVersion = 1;

        public int saveVersion = CurrentVersion;

        // ---- 玩家状态占位（S3 起由玩法系统填充，本冲刺只保证往返一致） ----
        public Vector3 playerPosition;
        public int realmTier;   // 境界大阶占位（0=炼气 …）
        public int realmLayer;  // 境界层数占位
        public string lastSceneId = "";

        // ---- 经济占位（C6/P1 反通胀：不可再生池是防篡改的核心保护对象 R11） ----
        public long spiritStones;             // 灵石
        public int lingmaiPoolRemaining;      // 不可再生池占位：灵脉残量
        public int bloodfieldPoolRemaining;   // 不可再生池占位：血田残量

        // ---- 元数据 ----
        public long savedAtUnixUtc;
    }

    /// <summary>
    /// 版本探针：解密后先用它读 saveVersion，再决定跑哪段升级链。
    /// JsonUtility 对缺失字段安全（保持默认值 0）。
    /// </summary>
    [Serializable]
    internal struct SaveVersionProbe
    {
        public int saveVersion;
    }
}
