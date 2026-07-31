// =============================================================
// 文件：SaveService.cs（E0-S5，ADR-007 / ADR-005）
// 作用：存档服务——槽位管理、原子写盘、.bak 回退、
//       读档流水线（验 MAC → 解密 → migration 升级链）。
// 设计：
//   - 纯 C# 实例类（非 MonoBehaviour），目录与密钥构造注入 → EditMode 全流程可测。
//   - 运行时默认实例用 CreateDefault()（persistentDataPath + SaveKeyProvider）。
//   - 读档任何失败路径【绝不抛未捕获异常】，返回 SaveLoadResult 结果码。
//   - dev 密钥档策略（P3 拍板）：警告不拒读——Debug.LogWarning 后照常读取。
// 写盘原子性：先写 <slot>.sav.tmp → File.Replace 原子替换（旧档轮转 .bak）。
//   首次写盘无旧档时用 File.Move（同样先落 tmp，无半成品主档窗口）。
// =============================================================

using System;
using System.IO;
using UnityEngine;

namespace MJ.Services
{
    public enum SaveLoadStatus
    {
        Success,            // 主档读取成功
        SuccessFromBackup,  // 主档坏/缺，.bak 回退成功
        NotFound,           // 主档与 .bak 均不存在
        Rejected            // 主档与 .bak 均无法通过校验（篡改/损坏/版本非法）
    }

    public sealed class SaveLoadResult
    {
        public SaveLoadStatus status;
        public bool usedDevKey;   // 档案由 dev 回退密钥写盘（P3：警告不拒读）
        public string error = "";

        public SaveLoadResult(SaveLoadStatus status, bool usedDevKey = false, string error = "")
        {
            this.status = status;
            this.usedDevKey = usedDevKey;
            this.error = error ?? "";
        }
    }

    public sealed class SaveService
    {
        private readonly string m_Directory;
        private readonly byte[] m_RootSecret;
        private readonly bool m_IsDevKey;

        /// <param name="directory">存档目录（测试注入临时目录；运行时用 CreateDefault）。</param>
        /// <param name="rootSecret">根密钥。</param>
        /// <param name="isDevKey">该密钥是否 dev 回退密钥（写盘时打容器头 dev 位）。</param>
        public SaveService(string directory, byte[] rootSecret, bool isDevKey)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("目录不能为空", nameof(directory));
            if (rootSecret == null || rootSecret.Length == 0) throw new ArgumentException("rootSecret 不能为空", nameof(rootSecret));
            m_Directory = directory;
            m_RootSecret = (byte[])rootSecret.Clone();
            m_IsDevKey = isDevKey;
        }

        /// <summary>运行时默认实例：persistentDataPath/Saves + SaveKeyProvider 当前密钥。</summary>
        public static SaveService CreateDefault()
        {
            return new SaveService(
                Path.Combine(Application.persistentDataPath, "Saves"),
                SaveKeyProvider.ActiveRootSecret,
                SaveKeyProvider.IsDevFallback);
        }

        public string GetSlotPath(int slot) => Path.Combine(m_Directory, "save_slot" + slot + ".sav");
        public string GetBackupPath(int slot) => GetSlotPath(slot) + ".bak";

        // ---------------- 写盘 ----------------

        public void Save(int slot, SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            data.saveVersion = SaveData.CurrentVersion; // 写盘恒为当前版本

            string json = JsonUtility.ToJson(data);
            byte[] container = SaveCrypto.Pack(json, m_RootSecret, m_IsDevKey);

            Directory.CreateDirectory(m_Directory);
            string path = GetSlotPath(slot);
            string tmp = path + ".tmp";
            string bak = GetBackupPath(slot);

            File.WriteAllBytes(tmp, container);
            if (File.Exists(path))
                File.Replace(tmp, path, bak); // 原子替换：旧主档轮转为 .bak
            else
                File.Move(tmp, path);         // 首次写盘：无旧档可轮转
        }

        // ---------------- 读盘 ----------------

        public SaveLoadResult TryLoad(int slot, out SaveData data)
        {
            data = null;
            string path = GetSlotPath(slot);
            string bak = GetBackupPath(slot);

            bool mainExists = File.Exists(path);
            bool bakExists = File.Exists(bak);
            if (!mainExists && !bakExists)
                return new SaveLoadResult(SaveLoadStatus.NotFound);

            string firstError = "";
            if (mainExists && TryLoadFile(path, out data, out bool devMain, out firstError))
                return new SaveLoadResult(SaveLoadStatus.Success, devMain);

            if (bakExists && TryLoadFile(bak, out data, out bool devBak, out string bakError))
            {
                Debug.LogWarning("[SaveService] 主档校验失败（" + firstError + "），已回退 .bak 读取槽位 " + slot + "。");
                return new SaveLoadResult(SaveLoadStatus.SuccessFromBackup, devBak);
            }

            data = null;
            return new SaveLoadResult(SaveLoadStatus.Rejected, false,
                mainExists ? "主档拒读：" + firstError : "主档缺失且 .bak 拒读");
        }

        /// <summary>单文件读档流水线。任何失败返回 false，绝不外抛异常。</summary>
        private bool TryLoadFile(string path, out SaveData data, out bool usedDevKey, out string error)
        {
            data = null;
            usedDevKey = false;
            error = "";
            try
            {
                byte[] container = File.ReadAllBytes(path);

                // 1) 头部速读 dev 位 → 选密钥（P3：dev 档警告不拒读）
                byte[] secret = m_RootSecret;
                if (SaveCrypto.TryReadDevFlag(container, out bool devFlag) && devFlag)
                {
                    usedDevKey = true;
                    secret = m_IsDevKey ? m_RootSecret : SaveKeyProvider.DevFallbackSecret;
                    Debug.LogWarning("[SaveService] 该存档由 dev 回退密钥写盘（非正式密钥），按 P3 策略警告后照常读取：" + path);
                }

                // 2) 验 MAC → 解密
                if (!SaveCrypto.TryUnpack(container, secret, out string json, out SaveCryptoError cryptoError))
                {
                    error = "容器校验失败：" + cryptoError;
                    return false;
                }

                // 3) 版本探针 → migration 升级链 → 强类型解析
                if (!SaveMigrations.TryParseAndMigrate(json, out data, out error))
                {
                    data = null;
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                // 读档健壮性红线：IO/解析等一切异常收敛为"拒读"
                data = null;
                error = "读档异常：" + e.GetType().Name + " " + e.Message;
                return false;
            }
        }
    }

    /// <summary>
    /// migration 升级链骨架（ADR-005）。
    /// v1 当前为链尾；未来每次 schema 变更：CurrentVersion+1 + 新增一个 case + 配套测试。
    /// </summary>
    public static class SaveMigrations
    {
        public static bool TryParseAndMigrate(string json, out SaveData data, out string error)
        {
            data = null;
            error = "";

            SaveVersionProbe probe;
            try
            {
                probe = JsonUtility.FromJson<SaveVersionProbe>(json);
            }
            catch (Exception e)
            {
                error = "JSON 解析失败：" + e.Message;
                return false;
            }

            int version = probe.saveVersion;
            if (version < 0 || version > SaveData.CurrentVersion)
            {
                error = "非法 saveVersion=" + version + "（当前=" + SaveData.CurrentVersion + "，未来版本拒读防降级注入）";
                return false;
            }

            // 逐级升级到当前版本
            while (version < SaveData.CurrentVersion)
            {
                json = UpgradeOneStep(version, json);
                version++;
            }

            try
            {
                data = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception e)
            {
                error = "升级后 JSON 解析失败：" + e.Message;
                return false;
            }
            if (data == null)
            {
                error = "升级后 JSON 解析为空";
                return false;
            }
            data.saveVersion = SaveData.CurrentVersion;
            return true;
        }

        /// <summary>单步升级：fromVersion → fromVersion+1。骨架期 v0→v1 为恒等升级（缺失字段取 SaveData 默认值）。</summary>
        private static string UpgradeOneStep(int fromVersion, string json)
        {
            switch (fromVersion)
            {
                case 0:
                    // v0 → v1：恒等升级骨架。v0 无新增/改名字段，JsonUtility
                    // 对缺失字段自动取默认值，无需改写 JSON 本体。
                    return json;
                default:
                    // 不可达：TryParseAndMigrate 已挡住 version > CurrentVersion
                    return json;
            }
        }
    }
}
