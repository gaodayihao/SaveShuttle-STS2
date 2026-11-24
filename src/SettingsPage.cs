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
                    () => Export(BackupService.KindFull, progressOnly: false),
                    ModSettingsButtonTone.Accent,
                    T("包含原版与 modded 两套进度、设置。Export every profile."));
                section.AddButton(
                    "export.progress",
                    T("仅导出进度"),
                    T("导出"),
                    () => Export(BackupService.KindProgress, progressOnly: true),
                    ModSettingsButtonTone.Normal,
                    T("只包含进度和当前局，不含设置。Only run/progress data."));
            })
            .AddSection("import", section =>
            {
                section.WithTitle(T("导入存档 Import"));
                section.WithDescription(T("导入前会自动备份当前存档；请先退到主菜单，导入后重启游戏生效。"));
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
                            () => DoImport(path, name),
                            ModSettingsButtonTone.Danger,
                            T(ImportService.Describe(info)));
                        index++;
                    }
                }
            })
            .AddSection("status", section =>
            {
                section.WithTitle(T("状态 Status"));
                section.AddParagraph("status.text", T("最近一次操作"), ModSettingsText.Dynamic(() => _status), null);
            });
    }

    private static void Export(string kind, bool progressOnly)
    {
        ExportResult result = BackupService.Export(kind, progressOnly);
        _status = result.Success
            ? "已导出 " + result.FileCount + " 个文件 → " + result.ArchivePath
            : "导出失败：" + result.Error;
        ModLog.Info(_status);
    }

    private static void DoImport(string path, string name)
    {
        ImportResult result = ImportService.Import(path);
        if (!result.Success)
        {
            _status = "导入失败：" + result.Error;
        }
        else
        {
            _status = "已从 " + name + " 恢复 " + result.FileCount + " 个文件；请重启游戏生效。旧存档备份："
                      + (result.PreviousBackupPath ?? "-");
        }
        ModLog.Info(_status);
    }

    public static string CurrentStatus => _status;
}
