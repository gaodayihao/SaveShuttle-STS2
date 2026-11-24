using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace SaveShuttle;

internal sealed class ArchiveInfo
{
    public string Path;
    public string FileName;
    public string CreatedAt;
    public string GameVersion;
    public string Kind;
    public long Size;
    public SaveArchiveManifest Manifest;
}

internal sealed class ImportResult
{
    public bool Success;
    public int FileCount;
    public string RestoredPath;
    public string PreviousBackupPath;
    public string Error;
    public List<string> Warnings = new List<string>();
}

internal static class ImportService
{
    private static readonly string[] EntryPrefixes = { "saves/", "configs/" };

    public static List<ArchiveInfo> ListArchives()
    {
        var list = new List<ArchiveInfo>();
        string root;
        try
        {
            root = PlatformPaths.ExportRoot();
        }
        catch (Exception)
        {
            return list;
        }

        if (!Directory.Exists(root))
        {
            return list;
        }

        foreach (string file in Directory.EnumerateFiles(root, "*.zip", SearchOption.TopDirectoryOnly))
        {
            var info = new ArchiveInfo
            {
                Path = file,
                FileName = Path.GetFileName(file),
                Size = new FileInfo(file).Length
            };
            try
            {
                using ZipArchive zip = ZipFile.OpenRead(file);
                ZipArchiveEntry manifestEntry = zip.GetEntry("manifest.json");
                if (manifestEntry != null)
                {
                    using StreamReader reader = new StreamReader(manifestEntry.Open());
                    info.Manifest = SaveArchiveManifest.FromJson(reader.ReadToEnd());
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("cannot read " + Path.GetFileName(file) + ": " + ex.GetType().Name);
            }

            info.CreatedAt = info.Manifest?.CreatedAt ?? File.GetLastWriteTime(file).ToString("yyyy-MM-ddTHH:mm:ss");
            info.GameVersion = info.Manifest?.GameVersion ?? "?";
            info.Kind = info.Manifest?.Kind ?? "?";
            list.Add(info);
        }

        return list
            .OrderByDescending(a => a.CreatedAt, StringComparer.Ordinal)
            .ThenByDescending(a => a.FileName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Human readable one line summary for a settings row.</summary>
    public static string Describe(ArchiveInfo info)
    {
        string size = info.Size >= 1024 * 1024
            ? (info.Size / 1048576.0).ToString("0.0") + " MB"
            : (info.Size / 1024.0).ToString("0") + " KB";
        return info.CreatedAt + "  ·  " + info.Kind + "  ·  " + info.GameVersion + "  ·  " + size;
    }

    /// <summary>
    /// Deletes older archives, keeping the newest <paramref name="keepFull"/> full/progress
    /// archives and the newest <paramref name="keepPreImport"/> pre-import safety copies.
    /// </summary>
    public static string PruneOld(int keepFull, int keepPreImport)
    {
        List<ArchiveInfo> all = ListArchives();
        var preImport = all.Where(a => a.Kind == BackupService.KindPreImport).ToList();
        var normal = all.Where(a => a.Kind != BackupService.KindPreImport).ToList();
        int removed = 0;
        long freed = 0;
        foreach (ArchiveInfo victim in normal.Skip(keepFull).Concat(preImport.Skip(keepPreImport)))
        {
            try
            {
                long size = new FileInfo(victim.Path).Length;
                File.Delete(victim.Path);
                removed++;
                freed += size;
            }
            catch (Exception ex)
            {
                ModLog.Warn("cannot delete " + victim.FileName + ": " + ex.GetType().Name);
            }
        }
        return "已删除 " + removed + " 个旧备份，释放 " + (freed / 1024) + " KB";
    }

    public static ImportResult Import(string archivePath)
    {
        var result = new ImportResult();
        try
        {
            if (!File.Exists(archivePath))
            {
                result.Error = "归档不存在 / archive not found";
                return result;
            }

            string userRoot = PlatformPaths.UserRoot();
            List<(ZipArchiveEntry Entry, string Target)> plan = new List<(ZipArchiveEntry, string)>();
            using (ZipArchive zip = ZipFile.OpenRead(archivePath))
            {
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    string prefix = Array.Find(EntryPrefixes, p => entry.FullName.StartsWith(p, StringComparison.Ordinal));
                    if (prefix == null)
                    {
                        continue;
                    }

                    string relative = entry.FullName.Substring(prefix.Length).Replace('\\', '/');
                    if (relative.Length == 0 || relative.Contains("..", StringComparison.Ordinal) || relative.StartsWith("/", StringComparison.Ordinal) || relative.Contains(":", StringComparison.Ordinal))
                    {
                        result.Error = "归档包含非法路径 / unsafe entry: " + entry.FullName;
                        return result;
                    }

                    string target = Path.GetFullPath(Path.Combine(userRoot, relative));
                    if (!target.StartsWith(Path.GetFullPath(userRoot) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    {
                        result.Error = "归档条目越界 / entry escapes save root: " + entry.FullName;
                        return result;
                    }
                    plan.Add((entry, target));
                }

                if (plan.Count == 0)
                {
                    result.Error = "归档里没有存档文件 / archive contains no save files";
                    return result;
                }

                ExportResult backup = BackupService.Export(BackupService.KindPreImport, progressOnly: false);
                if (!backup.Success)
                {
                    result.Error = "导入前备份失败，已中止 / pre-import backup failed: " + backup.Error;
                    return result;
                }
                result.PreviousBackupPath = backup.ArchivePath;

                foreach ((ZipArchiveEntry entry, string target) in plan)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using Stream input = entry.Open();
                    using FileStream output = File.Create(target);
                    input.CopyTo(output);
                }
                result.FileCount = plan.Count;
            }

            result.RestoredPath = userRoot;
            result.Success = true;
            ModLog.Info("imported " + result.FileCount + " file(s) from " + archivePath);
            return result;
        }
        catch (Exception ex)
        {
            result.Error = ex.GetType().Name + ": " + ex.Message;
            ModLog.Error("import failed: " + result.Error);
            return result;
        }
    }
}
