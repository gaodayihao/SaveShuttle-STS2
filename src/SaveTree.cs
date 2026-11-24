using System;
using System.Collections.Generic;
using System.IO;

namespace SaveShuttle;

/// <summary>Helpers for walking the on-disk save tree under user://default.</summary>
internal static class SaveTree
{
    public const string SavesFolder = "default";

    public static string RootPath => Path.Combine(PlatformPaths.UserRoot(), SavesFolder);

    public static bool Exists => Directory.Exists(RootPath);

    /// <summary>
    /// Enumerates save files. <paramref name="progressOnly"/> keeps run/progress data
    /// and escapes settings blobs (which are machine local and rarely worth migrating).
    /// </summary>
    public static IEnumerable<string> EnumerateFiles(bool progressOnly)
    {
        if (!Exists)
        {
            yield break;
        }

        foreach (string file in Directory.EnumerateFiles(RootPath, "*", SearchOption.AllDirectories))
        {
            string rel = Relative(file);
            if (progressOnly && !IsProgressFile(rel))
            {
                continue;
            }
            yield return file;
        }
    }

    public static string Relative(string absolutePath)
    {
        string rel = Path.GetRelativePath(PlatformPaths.UserRoot(), absolutePath);
        return rel.Replace('\\', '/');
    }

    public static bool IsProgressFile(string relativePath)
    {
        string lower = relativePath.ToLowerInvariant();
        if (lower.Contains("/saves/"))
        {
            return true;
        }
        return lower.EndsWith("/profile.save", StringComparison.Ordinal);
    }

    public static long TotalBytes(bool progressOnly)
    {
        long total = 0;
        foreach (string file in EnumerateFiles(progressOnly))
        {
            try
            {
                total += new FileInfo(file).Length;
            }
            catch (IOException)
            {
            }
        }
        return total;
    }
}
