using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using Godot;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;

namespace SaveShuttle;

internal sealed class ExportResult
{
    public bool Success;
    public string ArchivePath;
    public int FileCount;
    public long Bytes;
    public string Error;
    public bool Reused;
}

internal static class BackupService
{
    public const string KindFull = "full";
    public const string KindProgress = "progress";
    public const string KindPreImport = "preimport";

    private static string _lastSignature;
    private static string _lastArchivePath;
    private static DateTime _lastExportAt = DateTime.MinValue;

    public static ExportResult Export(string kind, bool progressOnly)
    {
        var result = new ExportResult();
        try
        {
            if (!SaveTree.Exists)
            {
                result.Error = "没有找到存档目录 / save directory not found";
                return result;
            }

            string signature = Signature(progressOnly);
            if (signature == _lastSignature
                && _lastArchivePath != null
                && kind != KindPreImport
                && (DateTime.UtcNow - _lastExportAt).TotalSeconds < 5.0
                && File.Exists(_lastArchivePath))
            {
                result.Success = true;
                result.Reused = true;
                result.ArchivePath = _lastArchivePath;
                ModLog.Info("export skipped: content unchanged since the previous export a moment ago");
                return result;
            }

            string root = PlatformPaths.ExportRoot();
            Directory.CreateDirectory(root);
            string fileName = "StS2_" + kind + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".zip";
            string zipPath = Path.Combine(root, fileName);

            var manifest = new SaveArchiveManifest
            {
                CreatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                Kind = kind,
                Platform = OS.GetName(),
                GameVersion = SafeGameVersion()
            };
            manifest.Mods.AddRange(LoadedMods());

            long bytes = 0;
            int count = 0;
            using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (string file in SaveTree.EnumerateFiles(progressOnly))
                {
                    string rel = SaveTree.Relative(file);
                    string entryName = "saves/" + rel;
                    using (Stream input = File.OpenRead(file))
                    {
                        ZipArchiveEntry entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
                        using (Stream output = entry.Open())
                        {
                            input.CopyTo(output);
                        }
                        bytes += input.Length;
                        count++;
                    }

                    manifest.Files.Add(new ManifestFile
                    {
                        Path = entryName,
                        Size = new FileInfo(file).Length,
                        Sha256 = Hash(file)
                    });
                }

                ZipArchiveEntry manifestEntry = zip.CreateEntry("manifest.json", CompressionLevel.Optimal);
                using (StreamWriter writer = new StreamWriter(manifestEntry.Open()))
                {
                    writer.Write(manifest.ToJson());
                }
            }

            result.Success = true;
            result.ArchivePath = zipPath;
            result.FileCount = count;
            result.Bytes = bytes;
            if (kind != KindPreImport)
            {
                _lastSignature = signature;
                _lastArchivePath = zipPath;
                _lastExportAt = DateTime.UtcNow;
            }
            ModLog.Info("exported " + count + " file(s), " + bytes + " bytes -> " + zipPath);
            return result;
        }
        catch (Exception ex)
        {
            result.Error = ex.GetType().Name + ": " + ex.Message;
            ModLog.Error("export failed: " + result.Error);
            return result;
        }
    }

    private static string Signature(bool progressOnly)
    {
        var builder = new System.Text.StringBuilder();
        foreach (string file in SaveTree.EnumerateFiles(progressOnly))
        {
            try
            {
                builder.Append(SaveTree.Relative(file)).Append(':').Append(new FileInfo(file).Length).Append(';');
            }
            catch (IOException)
            {
            }
        }
        return builder.ToString();
    }

    private static string SafeGameVersion()
    {
        try
        {
            return NGame.GetGameVersion();
        }
        catch (Exception)
        {
            return "unknown";
        }
    }

    private static IEnumerable<string> LoadedMods()
    {
        List<string> mods = new List<string>();
        try
        {
            foreach (Mod mod in ModManager.Mods)
            {
                if (mod?.manifest == null || mod.state != ModLoadState.Loaded)
                {
                    continue;
                }
                mods.Add(mod.manifest.id + "@" + (mod.manifest.version ?? "?"));
            }
        }
        catch (Exception)
        {
        }
        return mods.OrderBy(m => m, StringComparer.OrdinalIgnoreCase);
    }

    internal static string Hash(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
        {
            return Convert.ToHexString(sha.ComputeHash(stream));
        }
    }
}
