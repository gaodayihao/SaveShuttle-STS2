using System;
using System.IO;
using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace SaveShuttle;

/// <summary>
/// Resolves where archives are stored, without assuming a specific package name.
/// </summary>
internal static class PlatformPaths
{
    private const string FolderName = "save_exports";

    /// <summary>Private per-app data directory, i.e. what Godot calls user://.</summary>
    public static string UserRoot()
    {
        string path = ProjectSettings.GlobalizePath("user://").Replace('\\', '/').TrimEnd('/');
        if (string.IsNullOrEmpty(path))
        {
            path = OS.GetUserDataDir().Replace('\\', '/').TrimEnd('/');
        }
        return path;
    }

    /// <summary>
    /// Directory holding exported archives. Android prefers the app-specific external
    /// directory (visible to file managers and adb); desktop prefers the Downloads folder.
    /// Falls back to the private user directory when nothing else is writable.
    /// </summary>
    public static string ExportRoot()
    {
        string cached = _exportRoot;
        if (cached != null)
        {
            return cached;
        }

        foreach (string candidate in Candidates())
        {
            if (TryUse(candidate))
            {
                _exportRoot = candidate;
                ModLog.Info("archive directory: " + candidate);
                return candidate;
            }
        }

        string fallback = Path.Combine(UserRoot(), FolderName);
        Directory.CreateDirectory(fallback);
        _exportRoot = fallback;
        ModLog.Warn("using private fallback archive directory: " + fallback);
        return fallback;
    }

    private static string _exportRoot;

    private static bool TryUse(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            string probe = Path.Combine(dir, ".write_probe");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static System.Collections.Generic.IEnumerable<string> Candidates()
    {
        string os = OS.GetName();
        if (os == "Android")
        {
            foreach (string external in AndroidExternalFilesDirs())
            {
                yield return Path.Combine(external, FolderName);
            }
        }
        else
        {
            string downloads = DesktopDownloads();
            if (downloads != null)
            {
                yield return Path.Combine(downloads, "StS2Saves");
            }
        }
    }

    /// <summary>
    /// Android app-specific external files directory, derived at runtime from the
    /// package name reported by the system instead of a hardcoded package id.
    /// </summary>
    private static System.Collections.Generic.IEnumerable<string> AndroidExternalFilesDirs()
    {
        string package = PackageName();
        if (!string.IsNullOrEmpty(package))
        {
            yield return "/sdcard/Android/data/" + package + "/files";
            yield return "/storage/emulated/0/Android/data/" + package + "/files";
        }

        string modsDir = TryModsDirectory();
        if (!string.IsNullOrEmpty(modsDir) && modsDir.Replace('\\', '/').EndsWith("/mods", StringComparison.Ordinal))
        {
            yield return modsDir.Replace('\\', '/').Substring(0, modsDir.Replace('\\', '/').Length - "/mods".Length);
        }
    }

    /// <summary>
    /// Package id, read from the app private data path (/data/data/&lt;pkg&gt;/files) or from
    /// the Java process name. Returns null when neither is available.
    /// </summary>
    private static string PackageName()
    {
        string userRoot = UserRoot();
        foreach (string marker in new[] { "/data/data/", "/data/user/0/", "/data/user/" })
        {
            int index = userRoot.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0)
            {
                continue;
            }
            string rest = userRoot.Substring(index + marker.Length);
            int slash = rest.IndexOf('/');
            string candidate = slash >= 0 ? rest.Substring(0, slash) : rest;
            if (candidate.Length > 0)
            {
                return candidate;
            }
        }

        try
        {
            // Godot's Android backend sets the process name to the package id.
            string proc = File.Exists("/proc/self/cmdline") ? File.ReadAllText("/proc/self/cmdline") : "";
            int nul = proc.IndexOf('\0');
            if (nul > 0)
            {
                proc = proc.Substring(0, nul);
            }
            proc = proc.Trim();
            if (proc.Contains('.') && !proc.StartsWith("/", StringComparison.Ordinal))
            {
                return proc;
            }
        }
        catch (Exception)
        {
        }

        return null;
    }

    private static string TryModsDirectory()
    {
        try
        {
            return ModManager.ModsDirectory;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string DesktopDownloads()
    {
        try
        {
            string dir = OS.GetSystemDir(OS.SystemDir.Downloads);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                return dir;
            }
        }
        catch (Exception)
        {
        }

        try
        {
            string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home))
            {
                string downloads = Path.Combine(home, "Downloads");
                if (Directory.Exists(downloads))
                {
                    return downloads;
                }
            }
        }
        catch (Exception)
        {
        }

        return null;
    }
}
