// =============================================================
// 文件：SaveServiceTests.cs（EditMode 测试，无头 -batchmode -nographics 可跑）
// 作用：E0-S5 存档加密（ADR-007）验收测试。
//       覆盖：往返一致 / 反篡改（分区段参数化，各区段翻转 1 字节必拒读）/
//             .bak 回退 / migration 骨架 v0→v1 / dev 密钥「警告不拒读」（P3）/
//             原子写盘无 .tmp 残留 / 槽位不存在。
// 约定：不依赖渲染、不依赖场景，纯文件系统 + 纯 C# 加密逻辑。
//       全部写入系统临时目录，TearDown 清理，不污染工程与用户存档。
// =============================================================

using System;
using System.IO;
using System.Text;
using MJ.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class SaveServiceTests
{
    private string m_Dir;
    private byte[] m_Secret;      // 测试用"正式"根密钥（固定值，保证可复现）
    private SaveService m_Service;

    [SetUp]
    public void SetUp()
    {
        m_Dir = Path.Combine(Path.GetTempPath(), "mjfc_save_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(m_Dir);
        m_Secret = Encoding.UTF8.GetBytes("MJFC-TEST-ROOT-SECRET-0123456789");
        m_Service = new SaveService(m_Dir, m_Secret, isDevKey: false);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(m_Dir))
            Directory.Delete(m_Dir, recursive: true);
    }

    // ---------- 工具 ----------

    private static SaveData MakeSample()
    {
        return new SaveData
        {
            saveVersion = SaveData.CurrentVersion,
            playerPosition = new Vector3(-12.5f, 1.25f, 3.75f),
            realmTier = 1,
            realmLayer = 7,
            spiritStones = 4_2000L,
            lingmaiPoolRemaining = 88,
            bloodfieldPoolRemaining = 12,
            lastSceneId = "greybox_roomB",
            savedAtUnixUtc = 1730000000L
        };
    }

    private static void AssertDataEqual(SaveData expected, SaveData actual)
    {
        Assert.IsNotNull(actual);
        Assert.AreEqual(expected.saveVersion, actual.saveVersion);
        Assert.AreEqual(expected.playerPosition, actual.playerPosition);
        Assert.AreEqual(expected.realmTier, actual.realmTier);
        Assert.AreEqual(expected.realmLayer, actual.realmLayer);
        Assert.AreEqual(expected.spiritStones, actual.spiritStones);
        Assert.AreEqual(expected.lingmaiPoolRemaining, actual.lingmaiPoolRemaining);
        Assert.AreEqual(expected.bloodfieldPoolRemaining, actual.bloodfieldPoolRemaining);
        Assert.AreEqual(expected.lastSceneId, actual.lastSceneId);
        Assert.AreEqual(expected.savedAtUnixUtc, actual.savedAtUnixUtc);
    }

    // ---------- 验收 1：往返一致 ----------

    [Test]
    public void RoundTrip_AllFieldsEqual()
    {
        SaveData src = MakeSample();
        m_Service.Save(0, src);

        SaveLoadResult result = m_Service.TryLoad(0, out SaveData loaded);

        Assert.AreEqual(SaveLoadStatus.Success, result.status, result.error);
        AssertDataEqual(src, loaded);
    }

    // ---------- 验收 2：反篡改（分区段参数化，任意区段翻转 1 字节必拒读且不抛未捕获异常） ----------
    // 区段偏移与 SaveCrypto 容器布局常量对齐：
    //   MAGIC[0..3] | version[4] | flags[5] | salt[6..21] | IV[22..37] | MAC[38..69] | ciphertext[70..]

    [TestCase("Magic",      0)]
    [TestCase("Version",    SaveCrypto.VersionOffset)]
    [TestCase("Flags",      SaveCrypto.FlagsOffset)]
    [TestCase("Salt",       SaveCrypto.SaltOffset + 8)]
    [TestCase("IV",         SaveCrypto.IvOffset + 8)]
    [TestCase("MAC",        SaveCrypto.MacOffset + 16)]
    [TestCase("Ciphertext", SaveCrypto.HeaderSize + 4)]
    public void Tamper_FlipOneByte_IsRejected(string region, int offset)
    {
        m_Service.Save(0, MakeSample());
        string path = m_Service.GetSlotPath(0);

        byte[] bytes = File.ReadAllBytes(path);
        Assert.Greater(bytes.Length, offset, "容器长度必须覆盖被篡改区段：" + region);
        bytes[offset] ^= 0xFF; // 翻转 1 字节
        File.WriteAllBytes(path, bytes);

        SaveLoadResult result = null;
        SaveData loaded = null;
        Assert.DoesNotThrow(() => result = m_Service.TryLoad(0, out loaded),
            "篡改区段 " + region + " 不允许抛未捕获异常");
        Assert.AreEqual(SaveLoadStatus.Rejected, result.status,
            "篡改区段 " + region + " 必须拒读（无 .bak 时）");
        Assert.IsNull(loaded);
    }

    // ---------- 验收 2b：篡改后回退 .bak ----------

    [Test]
    public void Tamper_WithBackup_FallsBackToBak()
    {
        SaveData first = MakeSample();
        first.spiritStones = 111L;
        m_Service.Save(0, first);

        SaveData second = MakeSample();
        second.spiritStones = 222L;
        m_Service.Save(0, second); // 旧档轮转为 .bak（内容 = first）

        // 篡改主档密文区段
        string path = m_Service.GetSlotPath(0);
        byte[] bytes = File.ReadAllBytes(path);
        bytes[SaveCrypto.HeaderSize + 2] ^= 0xFF;
        File.WriteAllBytes(path, bytes);

        SaveLoadResult result = m_Service.TryLoad(0, out SaveData loaded);

        Assert.AreEqual(SaveLoadStatus.SuccessFromBackup, result.status, result.error);
        Assert.IsNotNull(loaded);
        Assert.AreEqual(111L, loaded.spiritStones, "回退档应为上一次成功写盘的内容");
    }

    // ---------- 验收 3：migration 骨架 v0 → v1 ----------

    [Test]
    public void Migration_V0Save_UpgradesToCurrentVersion()
    {
        // 手工构造 saveVersion=0 的旧档明文（字段子集，模拟旧 schema）
        string v0Json = "{\"saveVersion\":0,\"realmTier\":2,\"spiritStones\":999}";
        byte[] container = SaveCrypto.Pack(v0Json, m_Secret, devKey: false);
        File.WriteAllBytes(m_Service.GetSlotPath(0), container);

        SaveLoadResult result = m_Service.TryLoad(0, out SaveData loaded);

        Assert.AreEqual(SaveLoadStatus.Success, result.status, result.error);
        Assert.AreEqual(SaveData.CurrentVersion, loaded.saveVersion, "升级链必须推进到当前版本");
        Assert.AreEqual(2, loaded.realmTier);
        Assert.AreEqual(999L, loaded.spiritStones);
    }

    [Test]
    public void Migration_FutureVersion_IsRejected()
    {
        string futureJson = "{\"saveVersion\":99}";
        byte[] container = SaveCrypto.Pack(futureJson, m_Secret, devKey: false);
        File.WriteAllBytes(m_Service.GetSlotPath(0), container);

        SaveLoadResult result = m_Service.TryLoad(0, out SaveData loaded);

        Assert.AreEqual(SaveLoadStatus.Rejected, result.status, "未来版本档必须拒读（防降级注入）");
        Assert.IsNull(loaded);
    }

    // ---------- 验收 4（P3）：dev 密钥档「警告不拒读」 ----------

    [Test]
    public void DevKeySave_LoadWithReleaseService_WarnsButLoads()
    {
        var devService = new SaveService(m_Dir, SaveKeyProvider.DevFallbackSecret, isDevKey: true);
        SaveData src = MakeSample();
        devService.Save(0, src);

        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*dev.*"));
        SaveLoadResult result = m_Service.TryLoad(0, out SaveData loaded);

        Assert.AreEqual(SaveLoadStatus.Success, result.status, result.error);
        Assert.IsTrue(result.usedDevKey, "结果须标记 dev 密钥档");
        AssertDataEqual(src, loaded);
    }

    // ---------- 验收 5：原子写盘 ----------

    [Test]
    public void Save_LeavesNoTmpFile_AndRotatesBak()
    {
        m_Service.Save(0, MakeSample());
        Assert.IsFalse(File.Exists(m_Service.GetSlotPath(0) + ".tmp"), "不允许残留 .tmp");
        Assert.IsTrue(File.Exists(m_Service.GetSlotPath(0)));
        Assert.IsFalse(File.Exists(m_Service.GetBackupPath(0)), "首次写盘无 .bak");

        m_Service.Save(0, MakeSample());
        Assert.IsFalse(File.Exists(m_Service.GetSlotPath(0) + ".tmp"));
        Assert.IsTrue(File.Exists(m_Service.GetBackupPath(0)), "二次写盘旧档必须轮转为 .bak");
    }

    // ---------- 验收 6：槽位不存在 ----------

    [Test]
    public void Load_MissingSlot_ReturnsNotFound()
    {
        SaveLoadResult result = m_Service.TryLoad(7, out SaveData loaded);
        Assert.AreEqual(SaveLoadStatus.NotFound, result.status);
        Assert.IsNull(loaded);
    }

    // ---------- 补充：截断文件与空文件的健壮性 ----------

    [TestCase(0)]   // 空文件
    [TestCase(10)]  // 只剩半个头
    [TestCase(SaveCrypto.HeaderSize)] // 只有头无密文
    public void CorruptLength_IsRejectedWithoutThrow(int keepBytes)
    {
        m_Service.Save(0, MakeSample());
        string path = m_Service.GetSlotPath(0);
        byte[] bytes = File.ReadAllBytes(path);
        byte[] truncated = new byte[keepBytes];
        Array.Copy(bytes, truncated, keepBytes);
        File.WriteAllBytes(path, truncated);

        SaveLoadResult result = null;
        SaveData loaded = null;
        Assert.DoesNotThrow(() => result = m_Service.TryLoad(0, out loaded));
        Assert.AreEqual(SaveLoadStatus.Rejected, result.status);
        Assert.IsNull(loaded);
    }
}
