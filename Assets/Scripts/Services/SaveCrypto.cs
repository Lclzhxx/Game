// =============================================================
// 文件：SaveCrypto.cs（E0-S5，ADR-007）
// 作用：存档容器的加密/解密/认证——静态纯函数，独立可测（EditMode 无头可跑）。
// 方案（ADR-007 决定）：AES-256-CBC + HMAC-SHA256，Encrypt-then-MAC。
//   刻意【不用】AesGcm：Unity 2022.3 Mono 对 AesGcm 支持不稳（S2-R8），
//   CBC + HMAC 是 .NET Standard 2.0 时代 API，Mono/IL2CPP 全平台稳定。
//
// 容器格式 v1（在 ADR-007 基础上 +1 字节 flags，MAC 覆盖范围扩大到全头部）：
//   [4B]  MAGIC = "MJFC"
//   [1B]  containerVersion = 1（容器格式版本，独立于 saveVersion）
//   [1B]  flags（bit0 = devKey：由开发回退密钥写盘，见 SaveKeyProvider / P3）
//   [16B] salt   （每次写盘随机，PBKDF2 用）
//   [16B] IV     （每次写盘随机）
//   [32B] HMAC-SHA256（对 MAGIC|version|flags|salt|IV|ciphertext 计算——
//                      比 ADR 原文多覆盖了 6 字节头部，任何字节篡改都被 MAC 兜住）
//   [N B] ciphertext（AES-256-CBC(JSON UTF-8, PKCS7)）
//
// 密钥派生：PBKDF2(rootSecret, salt, 10_000 次) → 64B，
//   前 32B = AES key，后 32B = HMAC key（加密/认证密钥分离）。
//   PRF 用 .NET 默认 HMAC-SHA1（3 参构造），理由：HashAlgorithmName 重载在
//   部分 Mono 构建上行为需逐平台验证，而 PBKDF2-SHA1 无兼容风险；
//   本威胁模型（防随手改档，非对抗逆向）下强度足够，ADR-007 未钉定 PRF。
// 安全细节：HMAC 比较用常数时间；解密失败不泄露区分信息（统一 MacOrDecryptFailed 路径）。
// =============================================================

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MJ.Services
{
    public enum SaveCryptoError
    {
        None = 0,
        TooShort,
        BadMagic,
        BadContainerVersion,
        MacMismatch,
        DecryptFailed
    }

    public static class SaveCrypto
    {
        // ---- 容器布局常量（测试用它们定位篡改区段，勿改；改 = 容器版本 +1） ----
        public const int MagicOffset = 0;
        public const int MagicSize = 4;
        public const int VersionOffset = 4;
        public const int FlagsOffset = 5;
        public const int SaltOffset = 6;
        public const int SaltSize = 16;
        public const int IvOffset = SaltOffset + SaltSize;   // 22
        public const int IvSize = 16;
        public const int MacOffset = IvOffset + IvSize;      // 38
        public const int MacSize = 32;
        public const int HeaderSize = MacOffset + MacSize;   // 70

        public const byte ContainerVersion = 1;
        public const byte FlagDevKey = 1 << 0;
        public const int KdfIterations = 10_000;

        private static readonly byte[] Magic = { (byte)'M', (byte)'J', (byte)'F', (byte)'C' };

        // ---------------- 打包：JSON → 容器字节 ----------------

        public static byte[] Pack(string json, byte[] rootSecret, bool devKey)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            if (rootSecret == null || rootSecret.Length == 0) throw new ArgumentException("rootSecret 不能为空", nameof(rootSecret));

            byte[] salt = RandomBytes(SaltSize);
            byte[] iv = RandomBytes(IvSize);
            DeriveKeys(rootSecret, salt, out byte[] aesKey, out byte[] macKey);

            byte[] plaintext = Encoding.UTF8.GetBytes(json);
            byte[] ciphertext;
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = aesKey;
                aes.IV = iv;
                using (ICryptoTransform enc = aes.CreateEncryptor())
                    ciphertext = enc.TransformFinalBlock(plaintext, 0, plaintext.Length);
            }

            byte[] container = new byte[HeaderSize + ciphertext.Length];
            Buffer.BlockCopy(Magic, 0, container, MagicOffset, MagicSize);
            container[VersionOffset] = ContainerVersion;
            container[FlagsOffset] = devKey ? FlagDevKey : (byte)0;
            Buffer.BlockCopy(salt, 0, container, SaltOffset, SaltSize);
            Buffer.BlockCopy(iv, 0, container, IvOffset, IvSize);
            Buffer.BlockCopy(ciphertext, 0, container, HeaderSize, ciphertext.Length);

            byte[] mac = ComputeMac(macKey, container, ciphertext.Length);
            Buffer.BlockCopy(mac, 0, container, MacOffset, MacSize);
            return container;
        }

        // ---------------- 头部速读：不做任何解密，只取 dev 位 ----------------

        public static bool TryReadDevFlag(byte[] container, out bool devKey)
        {
            devKey = false;
            if (container == null || container.Length < HeaderSize) return false;
            for (int i = 0; i < MagicSize; i++)
                if (container[MagicOffset + i] != Magic[i]) return false;
            if (container[VersionOffset] != ContainerVersion) return false;
            devKey = (container[FlagsOffset] & FlagDevKey) != 0;
            return true;
        }

        // ---------------- 解包：容器字节 → JSON（先验 MAC 再解密） ----------------

        public static bool TryUnpack(byte[] container, byte[] rootSecret, out string json, out SaveCryptoError error)
        {
            json = null;

            if (container == null || container.Length < HeaderSize + 16) // 至少 1 个 AES block
            {
                error = SaveCryptoError.TooShort;
                return false;
            }
            for (int i = 0; i < MagicSize; i++)
            {
                if (container[MagicOffset + i] != Magic[i])
                {
                    error = SaveCryptoError.BadMagic;
                    return false;
                }
            }
            if (container[VersionOffset] != ContainerVersion)
            {
                error = SaveCryptoError.BadContainerVersion;
                return false;
            }

            byte[] salt = new byte[SaltSize];
            byte[] iv = new byte[IvSize];
            byte[] storedMac = new byte[MacSize];
            Buffer.BlockCopy(container, SaltOffset, salt, 0, SaltSize);
            Buffer.BlockCopy(container, IvOffset, iv, 0, IvSize);
            Buffer.BlockCopy(container, MacOffset, storedMac, 0, MacSize);
            int cipherLen = container.Length - HeaderSize;

            DeriveKeys(rootSecret, salt, out byte[] aesKey, out byte[] macKey);

            // 先验 MAC（Encrypt-then-MAC：MAC 不过绝不碰解密器）
            byte[] expectedMac = ComputeMac(macKey, container, cipherLen);
            if (!FixedTimeEquals(storedMac, expectedMac))
            {
                error = SaveCryptoError.MacMismatch;
                return false;
            }

            try
            {
                using (var aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = aesKey;
                    aes.IV = iv;
                    using (ICryptoTransform dec = aes.CreateDecryptor())
                    {
                        byte[] plaintext = dec.TransformFinalBlock(container, HeaderSize, cipherLen);
                        json = Encoding.UTF8.GetString(plaintext);
                    }
                }
            }
            catch (CryptographicException)
            {
                error = SaveCryptoError.DecryptFailed;
                return false;
            }

            error = SaveCryptoError.None;
            return true;
        }

        // ---------------- 内部工具 ----------------

        private static void DeriveKeys(byte[] rootSecret, byte[] salt, out byte[] aesKey, out byte[] macKey)
        {
            // 3 参构造 = PBKDF2-HMAC-SHA1（.NET 默认，Mono/IL2CPP 零兼容风险，见文件头说明）
            using (var kdf = new Rfc2898DeriveBytes(rootSecret, salt, KdfIterations))
            {
                byte[] block = kdf.GetBytes(64);
                aesKey = new byte[32];
                macKey = new byte[32];
                Buffer.BlockCopy(block, 0, aesKey, 0, 32);
                Buffer.BlockCopy(block, 32, macKey, 0, 32);
            }
        }

        /// <summary>MAC 覆盖：container[0 .. MacOffset) + container[HeaderSize .. HeaderSize+cipherLen)，即除 MAC 区段外的全部字节。</summary>
        private static byte[] ComputeMac(byte[] macKey, byte[] container, int cipherLen)
        {
            using (var hmac = new HMACSHA256(macKey))
            using (var ms = new MemoryStream(MacOffset + cipherLen))
            {
                ms.Write(container, 0, MacOffset);              // MAGIC|version|flags|salt|IV
                ms.Write(container, HeaderSize, cipherLen);     // ciphertext
                ms.Position = 0;
                return hmac.ComputeHash(ms);
            }
        }

        /// <summary>常数时间比较（防时序侧信道；不因首字节不同提前返回）。</summary>
        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private static byte[] RandomBytes(int count)
        {
            byte[] bytes = new byte[count];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return bytes;
        }
    }
}
