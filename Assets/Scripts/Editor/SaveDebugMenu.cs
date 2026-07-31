// =============================================================
// 文件：SaveDebugMenu.cs（E0-S5 子任务 5，ADR-007 调试入口）
// 作用：编辑器菜单 MJ → Save → …，把加密存档容器解开成明文 JSON 供人工排障。
// 菜单项：
//   MJ/Save/Dump Decrypted JSON        选文件 → 解密 → Console 打印 + 落 .decrypted.json
//   MJ/Save/Dump Slot 0 Decrypted JSON 直接取默认目录 slot0（最常用）
//   MJ/Save/Open Save Folder           打开 persistentDataPath/Saves
//   MJ/Save/Key Status                 打印当前生效密钥来源（正式注入 / dev 回退）
// 安全边界（红线）：
//   - 【仅编辑器】。本文件在 MJ.Editor 程序集（includePlatforms: Editor），永不进包。
//   - 明文 JSON 只写到 persistentDataPath 侧的 *.decrypted.json，【绝不写进 Assets/】，
//     避免明文存档被 Unity 导入并随仓库/构建外泄。
//   - 不打印任何密钥字节，只报告"密钥来源"。
// 密钥选择：优先当前生效密钥（CI 注入的正式密钥；编辑器非 Play 模式下通常是 dev 回退），
//   失败后自动重试 dev 回退密钥——两把都试过才判"拒读"，避免误报。
// 注意：需在 Unity 2022.3 下编译（使用 UnityEditor API）。
// =============================================================

using System;
using System.IO;
using System.Text;
using MJ.Services;
using UnityEditor;
using UnityEngine;

public static class SaveDebugMenu
{
    private const string MenuRoot = "MJ/Save/";

    private static string DefaultSaveDir => Path.Combine(Application.persistentDataPath, "Saves");

    // ---------------- 菜单项 ----------------

    [MenuItem(MenuRoot + "Dump Decrypted JSON", false, 10)]
    public static void DumpPickedFile()
    {
        string dir = Directory.Exists(DefaultSaveDir) ? DefaultSaveDir : Application.persistentDataPath;
        string path = EditorUtility.OpenFilePanel("选择存档容器（*.sav / *.sav.bak）", dir, "");
        if (string.IsNullOrEmpty(path)) return; // 用户取消
        Dump(path);
    }

    [MenuItem(MenuRoot + "Dump Slot 0 Decrypted JSON", false, 11)]
    public static void DumpSlot0()
    {
        string path = Path.Combine(DefaultSaveDir, "save_slot0.sav");
        if (!File.Exists(path))
        {
            Debug.LogWarning("[SaveDebugMenu] 槽位 0 主档不存在：" + path +
                             "（先在 Play 模式里存一次档，或用 Dump Decrypted JSON 手选文件）。");
            return;
        }
        Dump(path);
    }

    [MenuItem(MenuRoot + "Open Save Folder", false, 30)]
    public static void OpenSaveFolder()
    {
        string dir = DefaultSaveDir;
        Directory.CreateDirectory(dir);
        EditorUtility.RevealInFinder(dir);
        Debug.Log("[SaveDebugMenu] 存档目录：" + dir);
    }

    [MenuItem(MenuRoot + "Key Status", false, 31)]
    public static void KeyStatus()
    {
        // 只报告来源，不打印任何密钥字节。
        bool dev = SaveKeyProvider.IsDevFallback;
        Debug.Log("[SaveDebugMenu] 当前根密钥来源：" + (dev ? "dev 回退密钥（未注入正式密钥）" : "CI 注入的正式密钥") +
                  "\n说明：编辑器非 Play 模式下通常显示 dev——正式密钥由生成的 SaveSecret.cs 在 " +
                  "RuntimeInitializeOnLoadMethod 时注入，进 Play 模式后才生效。" +
                  "\n生成文件路径（gitignore，不入库）：Assets/Scripts/Services/Generated/SaveSecret.cs");
    }

    // ---------------- 核心：解密并落盘 ----------------

    private static void Dump(string path)
    {
        byte[] container;
        try
        {
            container = File.ReadAllBytes(path);
        }
        catch (Exception e)
        {
            Debug.LogError("[SaveDebugMenu] 读文件失败：" + path + " —— " + e.GetType().Name + " " + e.Message);
            return;
        }

        string header = DescribeHeader(container);

        // 先用当前生效密钥，失败再退 dev 回退密钥（两把都试过才判拒读）。
        string json;
        SaveCryptoError error;
        string keyUsed = SaveKeyProvider.IsDevFallback ? "dev 回退密钥" : "正式注入密钥";
        bool ok = SaveCrypto.TryUnpack(container, SaveKeyProvider.ActiveRootSecret, out json, out error);
        if (!ok && !SaveKeyProvider.IsDevFallback)
        {
            ok = SaveCrypto.TryUnpack(container, SaveKeyProvider.DevFallbackSecret, out json, out error);
            if (ok) keyUsed = "dev 回退密钥（正式密钥解不开，自动重试命中）";
        }

        if (!ok)
        {
            Debug.LogError("[SaveDebugMenu] 解密失败：" + error + "\n文件：" + path + "\n" + header +
                           "\n可能原因：档案被篡改/损坏，或写盘所用根密钥与本机当前密钥不一致。");
            return;
        }

        string pretty = PrettyPrintJson(json);
        Debug.Log("[SaveDebugMenu] 解密成功（" + keyUsed + "）\n文件：" + path + "\n" + header +
                  "\n---------------- 明文 JSON ----------------\n" + pretty);

        // 明文只落在存档目录侧，绝不写进 Assets/（防明文入库/入包）。
        try
        {
            string outPath = path + ".decrypted.json";
            File.WriteAllText(outPath, pretty, new UTF8Encoding(false));
            Debug.Log("[SaveDebugMenu] 明文已导出（调试用，勿入库）：" + outPath);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SaveDebugMenu] 明文导出失败（不影响上面的 Console 输出）：" +
                             e.GetType().Name + " " + e.Message);
        }
    }

    /// <summary>只读容器头部信息（不解密），用于排障时快速判断档案形态。</summary>
    private static string DescribeHeader(byte[] container)
    {
        if (container == null) return "容器：null";
        var sb = new StringBuilder();
        sb.Append("容器：").Append(container.Length).Append(" 字节");
        if (container.Length < SaveCrypto.HeaderSize)
        {
            sb.Append("（短于头部 ").Append(SaveCrypto.HeaderSize).Append(" 字节，非法容器）");
            return sb.ToString();
        }
        bool devFlag;
        if (SaveCrypto.TryReadDevFlag(container, out devFlag))
        {
            sb.Append("；MAGIC/版本合法；dev 位=").Append(devFlag ? "1（dev 密钥写盘）" : "0（正式密钥写盘）");
        }
        else
        {
            sb.Append("；MAGIC 或容器版本不合法");
        }
        return sb.ToString();
    }

    /// <summary>
    /// 极简 JSON 缩进器（JsonUtility 只吐压缩串，Console 里没法读）。
    /// 正确处理字符串字面量与转义，避免把字符串里的 {}/, 当结构符。
    /// </summary>
    private static string PrettyPrintJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return json ?? "";
        var sb = new StringBuilder(json.Length * 2);
        int indent = 0;
        bool inString = false;
        bool escaped = false;

        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];

            if (inString)
            {
                sb.Append(c);
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') inString = false;
                continue;
            }

            switch (c)
            {
                case '"':
                    inString = true;
                    sb.Append(c);
                    break;
                case '{':
                case '[':
                    sb.Append(c);
                    indent++;
                    NewLine(sb, indent);
                    break;
                case '}':
                case ']':
                    indent--;
                    NewLine(sb, indent);
                    sb.Append(c);
                    break;
                case ',':
                    sb.Append(c);
                    NewLine(sb, indent);
                    break;
                case ':':
                    sb.Append(": ");
                    break;
                default:
                    if (!char.IsWhiteSpace(c)) sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    private static void NewLine(StringBuilder sb, int indent)
    {
        sb.Append('\n');
        for (int i = 0; i < indent; i++) sb.Append("  ");
    }
}
