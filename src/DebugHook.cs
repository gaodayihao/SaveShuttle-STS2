using System;
using System.IO;
using System.Linq;
using Godot;

namespace SaveShuttle;

/// <summary>
/// Allows exercising export/import without touching the UI (useful for automated checks).
/// Activated only when a "savetest.txt" file sits next to the archive directory.
/// </summary>
internal static class DebugHook
{
    public static void Start()
    {
        Callable.From(Run).CallDeferred();
    }

    private static string ControlPath()
    {
        string root = PlatformPaths.ExportRoot();
        string parent = Path.GetDirectoryName(root) ?? root;
        return Path.Combine(parent, "savetest.txt");
    }

    private static void Run()
    {
        try
        {
            string control = ControlPath();
            if (!File.Exists(control))
            {
                return;
            }

            string[] lines = File.ReadAllLines(control).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            if (lines.Length == 0)
            {
                return;
            }

            string command = lines[0].Trim().ToLowerInvariant();
            ModLog.Info("[debug] command: " + command);
            switch (command)
            {
                case "export":
                    BackupService.Export(BackupService.KindFull, progressOnly: false);
                    break;
                case "export-progress":
                    BackupService.Export(BackupService.KindProgress, progressOnly: true);
                    break;
                case "corrupt":
                    {
                        string victim = Path.Combine(PlatformPaths.UserRoot(), "default", "1", "modded", "profile1", "saves", "progress.save");
                        if (File.Exists(victim))
                        {
                            File.WriteAllText(victim, "CORRUPTED-BY-TEST");
                            ModLog.Info("[debug] corrupted " + victim + " size=" + new FileInfo(victim).Length);
                        }
                        else
                        {
                            ModLog.Warn("[debug] nothing to corrupt at " + victim);
                        }
                        break;
                    }
                case "page":
                    {
                        if (STS2RitsuLib.Settings.ModSettingsRegistry.TryGetPage("SaveShuttle", "main", out STS2RitsuLib.Settings.ModSettingsPage page))
                        {
                            ModLog.Info("[debug] page title=" + page.Title?.Resolve() + " sections=" + page.Sections.Count);
                            foreach (STS2RitsuLib.Settings.ModSettingsSection section in page.Sections)
                            {
                                ModLog.Info("[debug]   section id=" + section.Id
                                            + " title=[" + (section.Title?.Resolve() ?? "") + "]"
                                            + " entries=" + section.Entries.Count);
                            }
                        }
                        else
                        {
                            ModLog.Warn("[debug] page not registered");
                        }
                        break;
                    }
                case "verify":
                    {
                        ArchiveInfo full = ImportService.ListArchives()
                            .FirstOrDefault(a => a.Manifest != null && a.Manifest.Kind == BackupService.KindFull);
                        if (full == null)
                        {
                            ModLog.Warn("[debug] no full archive to verify against");
                            break;
                        }
                        int ok = 0;
                        int bad = 0;
                        foreach (ManifestFile f in full.Manifest.Files)
                        {
                            string rel = f.Path.StartsWith("saves/", StringComparison.Ordinal) ? f.Path.Substring(6) : f.Path;
                            string disk = Path.Combine(PlatformPaths.UserRoot(), rel);
                            if (!File.Exists(disk))
                            {
                                bad++;
                                ModLog.Warn("[debug] missing " + rel);
                                continue;
                            }
                            string hash = BackupService.Hash(disk);
                            if (string.Equals(hash, f.Sha256, StringComparison.OrdinalIgnoreCase))
                            {
                                ok++;
                            }
                            else
                            {
                                bad++;
                                ModLog.Warn("[debug] hash mismatch " + rel);
                            }
                        }
                        ModLog.Info("[debug] verify ok=" + ok + " bad=" + bad + " (archive " + full.FileName + ")");
                        break;
                    }
                case "list":
                    foreach (ArchiveInfo info in ImportService.ListArchives())
                    {
                        ModLog.Info("[debug] archive " + info.FileName + " | " + ImportService.Describe(info));
                    }
                    break;
                case "import":
                    if (lines.Length > 1)
                    {
                        string name = lines[1].Trim();
                        ArchiveInfo target = ImportService.ListArchives()
                            .FirstOrDefault(a => a.FileName.Equals(name, StringComparison.OrdinalIgnoreCase))
                            ?? ImportService.ListArchives().FirstOrDefault();
                        if (target != null)
                        {
                            ImportResult result = ImportService.Import(target.Path);
                            ModLog.Info("[debug] import " + (result.Success ? "ok" : "failed: " + result.Error)
                                        + " files=" + result.FileCount + " backup=" + result.PreviousBackupPath);
                        }
                    }
                    break;
                default:
                    ModLog.Warn("[debug] unknown command " + command);
                    break;
            }
        }
        catch (Exception ex)
        {
            ModLog.Error("[debug] " + ex.GetType().Name + ": " + ex.Message);
        }
    }
}
