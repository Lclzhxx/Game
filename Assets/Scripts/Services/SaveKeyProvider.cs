// =============================================================
// 文件：SaveKeyProvider.cs（E0-S5，ADR-007 密钥管理）
// 作用：根密钥的单一取用点。
//   - 正式密钥：W2 由 CI 从 Secret `SAVE_ROOT_SECRET` 生成
//     Assets/Scripts/Services/Generated/SaveSecret.cs（gitignore，不进仓库），
//     该生成文件在静态构造/RuntimeInitializeOnLoad 中调用 InjectRootSecret()。
//   - 开发回退：无注入时使用 DevFallbackSecret（【公开常量，非机密】），
//     写盘时容器头打 dev 位；读取 dev 档采用「警告不拒读」策略（P3 拍板）。
// 红线：仓库内不得出现正式密钥字面量（E0-S5 验收：grep 不到密钥）。
// =============================================================

using System.Security.Cryptography;
using System.Text;

namespace MJ.Services
{
    public static class SaveKeyProvider
    {
        private static byte[] s_InjectedRootSecret;
        private static byte[] s_DevFallbackSecret;

        /// <summary>由 CI 生成的 SaveSecret.cs 调用，注入正式根密钥（W2 接线）。</summary>
        public static void InjectRootSecret(byte[] secret)
        {
            if (secret == null || secret.Length < 16)
            {
                UnityEngine.Debug.LogWarning("[SaveKeyProvider] 注入密钥无效（null 或 <16B），忽略，继续使用 dev 回退密钥。");
                return;
            }
            s_InjectedRootSecret = (byte[])secret.Clone();
        }

        /// <summary>当前生效的根密钥；无注入时为 dev 回退密钥。</summary>
        public static byte[] ActiveRootSecret => s_InjectedRootSecret ?? DevFallbackSecret;

        /// <summary>当前是否运行在 dev 回退密钥上（写盘时据此打 dev 位）。</summary>
        public static bool IsDevFallback => s_InjectedRootSecret == null;

        /// <summary>
        /// 开发回退密钥。【刻意公开、非机密】：仅保证开发期存档流程可跑通；
        /// dev 档在容器头带 dev 位，正式环境读到会 Debug.LogWarning（P3：警告不拒读）。
        /// </summary>
        public static byte[] DevFallbackSecret
        {
            get
            {
                if (s_DevFallbackSecret == null)
                {
                    using (var sha = SHA256.Create())
                        s_DevFallbackSecret = sha.ComputeHash(
                            Encoding.UTF8.GetBytes("MJFC-DEV-FALLBACK-KEY-NOT-A-SECRET"));
                }
                return s_DevFallbackSecret;
            }
        }
    }
}
