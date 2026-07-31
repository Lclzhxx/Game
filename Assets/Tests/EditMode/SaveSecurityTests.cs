// =============================================================
// 文件：SaveSecurityTests.cs（E0-S5 / ADR-007 安全护栏，EditMode 无头可跑）
// 作用：把 QA 计划 §8 两项补测落成源码断言（无渲染依赖，CI 可跑）：
//   1) 密钥不进仓库：.gitignore 含 SaveSecret.cs；生成文件被忽略且不入跟踪；
//      Assets/Scripts 下（除 gitignore 的 Generated/）搜不到 CI 生成的
//      Payload = "<44 字符 base64>" 字面量。dev 回退密钥是 SHA256("MJFC-DEV-...")
//      的派生值、非 base64 字面量，不会被误判。
//   2) S2-R8：SaveCrypto 不使用 AesGcm / ChaCha20（Mono 不稳），确用 AES-CBC + HMAC-SHA256。
// 设计：以 Application.dataPath 回溯仓库根；git 校验不可用时（无 git 环境）退化为
//       .gitignore 文件内容断言，保证本地无 git 也能守住主约束。
// 注意：需在 Unity 2022.3 下编译（EditMode 测试程序集）。
// =============================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class SaveSecurityTests
{
    /// <summary>仓库根：Application.dataPath = &lt;repo&gt;/Assets。</summary>
    private static string RepoRoot => Path.GetDirectoryName(Application.dataPath);

    private const string GeneratedSecretRel = "Assets/Scripts/Services/Generated/SaveSecret.cs";

    /// <summary>运行 git；返回退出码，git 不可用或异常时返回 null（调用方退化处理）。</summary>
    private static int? RunGit(string args, out string stdout)
    {
        stdout = "";
        try
        {
            var psi = new ProcessStartInfo("git", args)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var p = Process.Start(psi))
            {
                if (p == null) return null;
                stdout = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                return p.ExitCode;
            }
        }
        catch
        {
            return null;
        }
    }

    // ---------- 验收 1：密钥不进仓库 ----------

    [Test]
    public void Repo_GeneratedSaveSecret_IsGitIgnored()
    {
        string repo = RepoRoot;
        string gitignore = Path.Combine(repo, ".gitignore");
        Assert.IsTrue(File.Exists(gitignore), ".gitignore 缺失：无法守护「密钥不进仓库」约束");
        string gi = File.ReadAllText(gitignore);
        Assert.IsTrue(gi.Contains("SaveSecret.cs"),
            ".gitignore 必须忽略 SaveSecret.cs（E0-S5：CI 注入的正式根密钥绝不入库）");
        Assert.IsTrue(gi.Contains("Assets/Scripts/Services/Generated/"),
            ".gitignore 必须忽略整个 Generated/ 目录（含其自动生成的 .meta）");

        string abs = Path.Combine(repo, GeneratedSecretRel.Replace('/', Path.DirectorySeparatorChar));

        // 主守卫：git 必须判定该路径被忽略（exit 0 = 忽略）。
        int? ignored = RunGit($"check-ignore \"{abs}\"", out _);
        if (ignored.HasValue)
            Assert.AreEqual(0, ignored.Value,
                $"{GeneratedSecretRel} 必须被 git 忽略（git check-ignore 返回非 0）。密钥一旦进 git 历史清除成本极高。");

        // 次守卫：该路径绝不能被 git 跟踪（exit 0 = 已跟踪，必须失败）。
        int? tracked = RunGit($"ls-files --error-unmatch \"{abs}\"", out _);
        if (tracked.HasValue)
            Assert.AreNotEqual(0, tracked.Value,
                $"{GeneratedSecretRel} 绝不能被 git 跟踪。请确认其已被 .gitignore 覆盖后再提交。");
    }

    [Test]
    public void Repo_NoCommittedKeyLiteral()
    {
        string scripts = Path.Combine(RepoRoot, "Assets", "Scripts");
        Assert.IsTrue(Directory.Exists(scripts), "Assets/Scripts 缺失");

        // CI 生成的密钥字面量形如：private const string Payload = "<44 字符 base64>"；
        // 该模式唯一对应仓库 Secret 的 Base64(SHA-256)，且只应出现在 gitignore 的 Generated/ 下。
        var re = new Regex(@"Payload\s*=\s*""([A-Za-z0-9+/]{43}[A-Za-z0-9+/=])""");
        var offenders = new List<string>();
        foreach (var f in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string norm = f.Replace('\\', '/');
            if (norm.Contains("/Generated/")) continue; // 生成目录已被 gitignore，CI 会再拦
            if (re.IsMatch(File.ReadAllText(f)))
                offenders.Add(norm);
        }
        Assert.IsEmpty(offenders,
            "仓库源码中发现疑似 CI 密钥字面量（Payload = 44 字符 base64），位于：\n" +
            string.Join("\n", offenders) + "\n密钥绝不能进 git 历史。");
    }

    // ---------- 验收 2：S2-R8 不使用 AesGcm / ChaCha20 ----------

    [Test]
    public void SaveCrypto_DoesNotUseAesGcmOrChaCha20()
    {
        string path = Path.Combine(RepoRoot, "Assets", "Scripts", "Services", "SaveCrypto.cs");
        Assert.IsTrue(File.Exists(path), "SaveCrypto.cs 缺失");
        string src = File.ReadAllText(path);

        Assert.IsFalse(src.Contains("AesGcm"),
            "S2-R8：SaveCrypto 禁止 AesGcm（Unity 2022.3 Mono 支持不稳），须用 AES-CBC + HMAC-SHA256（ADR-007）");
        Assert.IsFalse(src.Contains("ChaCha20"),
            "S2-R8：SaveCrypto 禁止 ChaCha20");

        // 反向确认确实采用了批准的基元（避免「只是没写 AesGcm」的空断言）。
        Assert.IsTrue(src.Contains("CipherMode.CBC"), "SaveCrypto 应使用 AES-CBC（ADR-007）");
        Assert.IsTrue(src.Contains("HMACSHA256"), "SaveCrypto 应使用 HMAC-SHA256（ADR-007 Encrypt-then-MAC）");
    }
}
