using System;
using System.Collections.Generic;
using STS2RitsuLib.Settings;

namespace SaveShuttle;

internal static class SettingsPage
{
    private const string ModId = "SaveShuttle";
    private const string PageId = "main";
    private static string _status = "";

    private static ModSettingsText T(string text) => ModSettingsText.Literal(text);

    public static void Register()
    {
        ModSettingsRegistry.RegisterModDisplayName(ModId, T("存档穿梭 SaveShuttle"));
        ModSettingsRegistry.Register(ModId, Build, PageId);
        ModLog.Info("settings page registered");
    }

    private static void Build(ModSettingsPageBuilder page)
    {
        page.WithTitle(T("存档穿梭 SaveShuttle"))
            .WithDescription(T("导出/导入存档备份。命令在模组配置界面执行，导入后需要重启游戏。"))
            .AddSection("export", section =>
            {
                section.WithTitle(T("导出存档 Export"));
                section.WithDescription(T("把存档打包成 zip 并保存到备份目录"));
                section.AddButton(
                    "export.full",
                    T("导出全部存档"),
                    T("导出"),
                    host => Export(host, BackupService.KindFull, progressOnly: false),
                    ModSettingsButtonTone.Accent,
                    T("包含原版与 modded 两套进度、设置。Export every profile."));
                section.AddButton(
                    "export.progress",
                    T("仅导出进度"),
                    T("导出"),
                    host => Export(host, BackupService.KindProgress, progressOnly: true),
                    ModSettingsButtonTone.Normal,
                    T("只包含进度和当前局，不含设置。Only run/progress data."));
                section.AddParagraph("export.status", T("最近一次操作"), ModSettingsText.Dynamic(() => _status), null);
            })
            .AddSection("import", section =>
            {
                section.WithTitle(T("导入存档 Import"));
                section.WithDescription(T("导入前会校验归档完整性并自动备份当前存档；请先退到主菜单，导入后重启游戏生效。"));
                List<ArchiveInfo> archives = ImportService.ListArchives();
                if (archives.Count == 0)
                {
                    section.AddParagraph(
                        "import.empty",
                        T("还没有备份"),
                        T("先在「导出」里创建一个，或把 zip 放进备份目录后重新打开本页。"),
                        null);
                }
                else
                {
                    int index = 0;
                    foreach (ArchiveInfo info in archives)
                    {
                        if (index >= 6)
                        {
                            break;
                        }
                        string path = info.Path;
                        string name = info.FileName;
                        section.AddButton(
                            "import." + index,
                            T(name),
                            T("导入"),
                            host => DoImport(host, path, name),
                            ModSettingsButtonTone.Danger,
                            T(ImportService.Describe(info)));
                        index++;
                    }
                }
            })
            .AddSection("force", section =>
            {
                section.WithTitle(T("强制导入（跳过校验）Force import"));
                section.WithDescription(T("给手工改过的归档用：不比对 sha256，直接覆盖。导入前仍会自动备份。"));
                List<ArchiveInfo> forceList = ImportService.ListArchives();
                int forceIndex = 0;
                foreach (ArchiveInfo info in forceList)
                {
                    if (forceIndex >= 6)
                    {
                        break;
                    }
                    string path = info.Path;
                    string name = info.FileName;
                    section.AddButton(
                        "force." + forceIndex,
                        T(name),
                        T("强制导入"),
                        host => DoImport(host, path, name, skipIntegrityCheck: true),
                        ModSettingsButtonTone.Danger,
                        T(ImportService.Describe(info)));
                    forceIndex++;
                }
                if (forceList.Count == 0)
                {
                    section.AddParagraph("force.empty", T("还没有备份"), T("先导出一个再回来。"), null);
                }
            })
            .AddSection("maintenance", section =>
            {
                section.WithTitle(T("维护 Maintenance"));
                section.WithDescription(T("清理旧备份：保留最近 10 个正常备份和 5 个导入前快照。"));
                section.AddButton(
                    "maintenance.prune",
                    T("清理旧备份"),
                    T("清理"),
                    host =>
                    {
                        _status = ImportService.PruneOld(keepFull: 10, keepPreImport: 5);
                        ModLog.Info(_status);
                        Refresh(host);
                    },
                    ModSettingsButtonTone.Danger,
                    T("删除更早的归档，释放空间。Deletes older archives."));
            })
            .AddSection("status", section =>
            {
                section.WithTitle(T("状态 Status"));
                section.AddParagraph("status.text", T("最近一次操作"), ModSettingsText.Dynamic(() => _status), null);
            });
    }

    private static void Export(IModSettingsUiActionHost host, string kind, bool progressOnly)
    {
        ExportResult result = BackupService.Export(kind, progressOnly);
        if (!result.Success)
        {
            _status = "导出失败：" + result.Error;
        }
        else if (result.Reused)
        {
            _status = "内容与几秒前的备份相同，直接沿用：" + Path.GetFileName(result.ArchivePath);
        }
        else
        {
            _status = "已导出 " + result.FileCount + " 个文件 → " + Path.GetFileName(result.ArchivePath);
        }
        ModLog.Info(_status);
        Refresh(host);
    }

    private static void DoImport(IModSettingsUiActionHost host, string path, string name, bool skipIntegrityCheck = false)
    {
        ImportResult result = ImportService.Import(path, skipIntegrityCheck);
        if (!result.Success)
        {
            _status = (skipIntegrityCheck ? "强制导入失败：" : "导入失败：") + result.Error;
        }
        else
        {
            _status = "已从 " + name + " 恢复 " + result.FileCount + " 个文件；请重启游戏生效。旧存档备份："
                      + (result.PreviousBackupPath == null ? "-" : Path.GetFileName(result.PreviousBackupPath));
        }
        ModLog.Info(_status);
        Refresh(host);
    }

    /// <summary>Rebuild the page so the status line and the archive list reflect what just happened.</summary>
    private static void Refresh(IModSettingsUiActionHost host)
    {
        try
        {
            host?.RequestRefreshAfterDataModelBatchChange();
            host?.RequestRefresh();
        }
        catch (Exception ex)
        {
            ModLog.Warn("refresh failed: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    public static string CurrentStatus => _status;
}
