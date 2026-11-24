using System;
using System.Collections.Generic;
using System.IO;

namespace SaveShuttle;

/// <summary>
/// Per-mod configuration that lives next to the save tree (user://mods, user://mod_configs, ...).
/// These are invisible from the external mods folder, so they are worth backing up too.
/// </summary>
internal static class ConfigTree
{
    /// <summary>Directories that hold regenerable caches or logs, never worth exporting.</summary>
    private static readonly HashSet<string> _ignoredDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "default",
        "logs",
        "vulkan",
        "shader_cache",
        "localization_override"
    };

    public static bool IsIgnoredFile(string fileName)
    {
        return fileName.EndsWith(".lock", StringComparison.OrdinalIgnoreCase)
               || fileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("profileInstalled", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("vfx_warmup.done", StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<string> EnumerateFiles()
    {
        string root = PlatformPaths.UserRoot();
        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
        {
            if (!IsIgnoredFile(Path.GetFileName(file)))
            {
                yield return file;
            }
        }

        foreach (string dir in Directory.GetDirectories(root))
        {
            if (_ignoredDirs.Contains(Path.GetFileName(dir)))
            {
                continue;
            }

            // Skip trees that are obviously caches even when the folder name is new.
            foreach (string file in SafeEnumerate(dir))
            {
                if (!IsIgnoredFile(Path.GetFileName(file)))
                {
                    yield return file;
                }
            }
        }
    }

    private static IEnumerable<string> SafeEnumerate(string dir)
    {
        try
        {
            return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories);
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }

    public static string Relative(string absolutePath)
    {
        return Path.GetRelativePath(PlatformPaths.UserRoot(), absolutePath).Replace('\\', '/');
    }
}
