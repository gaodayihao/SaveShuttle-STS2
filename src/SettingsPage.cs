using System;
using System.Collections.Generic;
using Godot;
using STS2RitsuLib.Settings;

namespace SaveShuttle;

internal static class SettingsPage
{
    private const string ModId = "SaveShuttle";
    private const string MainPageId = "main";
    private const string ImportPageId = "import";
    private const int MaxRows = 12;

    private static ModSettingsText T(string text) => ModSettingsText.Literal(text);

    public static void Register()
    {
        ModSettingsRegistry.RegisterModDisplayName(ModId, T("存档穿梭 SaveShuttle"));
        ModSettingsRegistry.Register(ModId, BuildMain, MainPageId);
        ModSettingsRegistry.Register(ModId, BuildImport, ImportPageId);
        ModLog.Info("settings pages registered");
    }

    private static void BuildMain(ModSettingsPageBuilder page)
    {
        page.WithTitle(T("存档穿梭 SaveShuttle"))
            .WithDescription(T("导出/导入存档与 mod 配置。所有操作都会在屏幕下方给出提示，导入后需要重启游戏。"))
            .AddSection("export", section =>
            {
                section.WithTitle(T("导出存档 Export"));
                section.WithDescription(T("打包到备份目录，Android 上是 files/save_exports/。"));
                section.AddButton(
                    "export.full",
                    T("导出全部存档"),
                    T("导出"),
                    host => Export(host, BackupService.KindFull, progressOnly: false),
                    ModSettingsButtonTone.Accent,
                    T("存档 + 所有 mod 配置（TheFoolQOL、JmcModLib、mod_configs 等）"));
                section.AddButton(
                    "export.progress",
                    T("仅导出进度"),
                    T("导出"),
                    host => Export(host, BackupService.KindProgress, progressOnly: true),
                    ModSettingsButtonTone.Normal,
                    T("只包含进度和当前局，不含 mod 配置"));
            })
            .AddSection("import_link", section =>
            {
                section.WithTitle(T("导入存档 Import"));
                section.WithDescription(T("进入子页面选择要导入的备份；每次进入都会重新扫描备份目录，所以刚从上面导出的文件会出现在里面。"));
                section.AddSubpage(
                    "open.import",
                    T("打开导入列表"),
                    ImportPageId,
                    T("导入存档 Import"),
                    T("选择要恢复的备份"));
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
                        Publish(ImportService.PruneOld(keepFull: 10, keepPreImport: 5));
                    },
                    ModSettingsButtonTone.Danger,
                    T("删除更早的归档，释放空间"));
            });
    }

    private static void BuildImport(ModSettingsPageBuilder page)
    {
        List<ArchiveInfo> archives = ImportService.ListArchives();
        page.WithTitle(T("导入存档 Import"))
            .AsChildOf(MainPageId)
            .WithDescription(T("「导入」会先校验归档完整性；「强制导入」跳过校验，用于手工改过的归档。两者都会先备份当前存档。"));

        page.AddSection("list", section =>
        {
            section.WithTitle(T("共 " + archives.Count + " 个备份"));
            section.AddButton(
                "refresh",
                T("刷新列表"),
                T("刷新"),
                host => { RefreshImportPage(); Publish("已重新扫描备份目录"); },
                ModSettingsButtonTone.Normal,
                T("备份目录被外部改动（比如从电脑拷入 zip）时使用"));
            if (archives.Count == 0)
            {
                section.AddParagraph(
                    "empty",
                    T("还没有备份"),
                    T("回到上一页点「导出全部存档」即可创建第一个备份。"),
                    null);
                return;
            }

            int shown = Math.Min(archives.Count, MaxRows);
            for (int i = 0; i < shown; i++)
            {
                ArchiveInfo info = archives[i];
                section.AddCustom(
                    "archive." + i,
                    T(""),
                    host => ArchiveRow(host, info),
                    T(""),
                    null);
            }
        });
    }

    /// <summary>
    /// One archive row. RitsuLib's AddCustom renders only the control we return, so the
    /// file name and metadata are drawn here instead of relying on the row template.
    /// </summary>
    private static Control ArchiveRow(IModSettingsUiActionHost host, ArchiveInfo info)
    {
        HBoxContainer row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0f, 72f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 12);

        VBoxContainer texts = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        texts.AddThemeConstantOverride("separation", 2);

        Label name = new Label
        {
            Text = info.FileName,
            AutowrapMode = TextServer.AutowrapMode.Arbitrary,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        name.AddThemeFontSizeOverride("font_size", 21);

        Label meta = new Label
        {
            Text = ImportService.Describe(info),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        meta.AddThemeFontSizeOverride("font_size", 15);
        meta.AddThemeColorOverride("font_color", new Color(0.72f, 0.76f, 0.85f));

        texts.AddChild(name);
        texts.AddChild(meta);
        row.AddChild(texts);

        Button normal = new Button
        {
            Text = "导入",
            CustomMinimumSize = new Vector2(96f, 48f),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        normal.Pressed += () => DoImport(host, info, skipIntegrityCheck: false);

        Button force = new Button
        {
            Text = "强制导入",
            CustomMinimumSize = new Vector2(124f, 48f),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        force.Pressed += () => DoImport(host, info, skipIntegrityCheck: true);

        row.AddChild(normal);
        row.AddChild(force);
        return row;
    }

    /// <summary>
    /// RitsuLib builds a page once at registration, so the archive snapshot has to be
    /// re-registered whenever the backup directory changes.
    /// </summary>
    private static void RefreshImportPage()
    {
        try
        {
            ModSettingsRegistry.Register(ModId, BuildImport, ImportPageId);
            ModLog.Info("import page re-registered (" + ImportService.ListArchives().Count + " archives)");
        }
        catch (Exception ex)
        {
            ModLog.Warn("re-registering the import page failed: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>Debug entry point: run the real export path without a UI host.</summary>
    internal static void DebugExportAll()
    {
        Export(null, BackupService.KindFull, progressOnly: false);
    }

    private static void Publish(string message, bool error = false)
    {
        ModLog.Info(message);
        Toast.Show(message, error);
    }

    private static void Export(IModSettingsUiActionHost host, string kind, bool progressOnly)
    {
        ExportResult result = BackupService.Export(kind, progressOnly);
        if (!result.Success)
        {
            Publish("导出失败：" + result.Error, error: true);
        }
        else if (result.Reused)
        {
            Publish("内容与几秒前的备份相同，直接沿用：" + System.IO.Path.GetFileName(result.ArchivePath));
        }
        else
        {
            RefreshImportPage();
            Publish("已导出 " + result.FileCount + " 个文件 → " + System.IO.Path.GetFileName(result.ArchivePath)
                    + "（在「打开导入列表」里可见）");
        }
    }

    private static void DoImport(IModSettingsUiActionHost host, ArchiveInfo info, bool skipIntegrityCheck)
    {
        ImportResult result = ImportService.Import(info.Path, skipIntegrityCheck);
        if (!result.Success)
        {
            Publish((skipIntegrityCheck ? "强制导入失败：" : "导入失败：") + result.Error, error: true);
            return;
        }

        Publish("已从 " + info.FileName + " 恢复 " + result.FileCount + " 个文件；请重启游戏生效。旧存档备份："
                + (result.PreviousBackupPath == null ? "-" : System.IO.Path.GetFileName(result.PreviousBackupPath)));
    }
}
