using System;
using System.Collections.Generic;
using Godot;
using STS2RitsuLib.Settings;

namespace SaveShuttle;

internal static class SettingsPage
{
    private const string ModId = "SaveShuttle";
    private const string PageId = "main";
    private static string _status = "";
    private static readonly IModSettingsValueBinding<string> _statusBinding =
        ModSettingsBindings.InMemory<string>(ModId, "last_action", "");

    private const int ArchiveSlots = 6;

    /// <summary>Bumped after every export/import so the archive rows re-resolve.</summary>
    private static readonly IModSettingsValueBinding<int> _listRevision =
        ModSettingsBindings.InMemory<int>(ModId, "list_revision", 0);

    private static List<ArchiveInfo> _archives = ImportService.ListArchives();

    private static ModSettingsText T(string text) => ModSettingsText.Literal(text);

    private static ArchiveInfo Slot(int index) =>
        index >= 0 && index < _archives.Count ? _archives[index] : null;

    private static void ReloadArchives()
    {
        _archives = ImportService.ListArchives();
        try
        {
            _listRevision.Write(_listRevision.Read() + 1);
        }
        catch (Exception)
        {
        }
    }

    private static ModSettingsText SlotName(int index) =>
        ModSettingsText.Dynamic(() => Slot(index)?.FileName ?? "（空 Empty）", new IModSettingsBinding[] { _listRevision });

    private static ModSettingsText SlotInfo(int index) =>
        ModSettingsText.Dynamic(
            () =>
            {
                ArchiveInfo info = Slot(index);
                return info == null ? "—" : ImportService.Describe(info);
            },
            new IModSettingsBinding[] { _listRevision });

    private static ModSettingsText StatusText() =>
        ModSettingsText.Dynamic(() => _status, new IModSettingsBinding[] { _statusBinding });

    private static void Publish(string message, bool error = false)
    {
        _status = message;
        try
        {
            _statusBinding.Write(message);
        }
        catch (Exception)
        {
            // status text is cosmetic; never let it break the action
        }
        Toast.Show(message, error);
    }

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
                section.AddParagraph("export.status", T("最近一次操作"), StatusText(), null);
            })
            .AddSection("import", section =>
            {
                section.WithTitle(T("导入存档 Import"));
                section.WithDescription(T("「导入」会先校验归档完整性；「强制导入」跳过校验，用于手工改过的归档。两者都会先备份当前存档，导入后需重启游戏。"));
                ReloadArchives();
                int rows = Math.Min(_archives.Count, ArchiveSlots * 2);
                for (int slot = 0; slot < rows; slot++)
                {
                    int index = slot;
                    section.AddCustom(
                        "import." + index,
                        SlotName(index),
                        host => ArchiveRow(host, index),
                        SlotInfo(index),
                        () => Slot(index) != null);
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
                        Publish(ImportService.PruneOld(keepFull: 10, keepPreImport: 5));
                        Refresh(host);
                    },
                    ModSettingsButtonTone.Danger,
                    T("删除更早的归档，释放空间。Deletes older archives."));
            })
            .AddSection("status", section =>
            {
                section.WithTitle(T("状态 Status"));
                section.AddParagraph("status.text", T("最近一次操作"), StatusText(), null);
            });
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
            Publish("内容与几秒前的备份相同，直接沿用：" + Path.GetFileName(result.ArchivePath));
        }
        else
        {
            Publish("已导出 " + result.FileCount + " 个文件 → " + Path.GetFileName(result.ArchivePath));
        }
        Refresh(host);
    }

    private static void DoImport(IModSettingsUiActionHost host, string path, string name, bool skipIntegrityCheck = false)
    {
        ImportResult result = ImportService.Import(path, skipIntegrityCheck);
        if (!result.Success)
        {
            Publish((skipIntegrityCheck ? "强制导入失败：" : "导入失败：") + result.Error, error: true);
        }
        else
        {
            Publish("已从 " + name + " 恢复 " + result.FileCount + " 个文件；请重启游戏生效。旧存档备份："
                    + (result.PreviousBackupPath == null ? "-" : Path.GetFileName(result.PreviousBackupPath)));
        }
        Refresh(host);
    }

    /// <summary>One archive row: the name on the left, both import buttons on the right.</summary>
    private static Control ArchiveRow(IModSettingsUiActionHost host, int index)
    {
        HBoxContainer box = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        box.AddThemeConstantOverride("separation", 10);

        Button normal = new Button { Text = "导入", CustomMinimumSize = new Vector2(96f, 44f) };
        normal.Pressed += () =>
        {
            ArchiveInfo info = Slot(index);
            if (info == null)
            {
                Publish("这个位置还没有备份", error: true);
                return;
            }
            DoImport(host, info.Path, info.FileName);
        };

        Button force = new Button { Text = "强制导入", CustomMinimumSize = new Vector2(120f, 44f) };
        force.Pressed += () =>
        {
            ArchiveInfo info = Slot(index);
            if (info == null)
            {
                Publish("这个位置还没有备份", error: true);
                return;
            }
            DoImport(host, info.Path, info.FileName, skipIntegrityCheck: true);
        };

        box.AddChild(normal);
        box.AddChild(force);
        return box;
    }

    private static void ScheduleRebuild(IModSettingsUiActionHost host)
    {
        try
        {
            if (Engine.GetMainLoop() is not SceneTree tree)
            {
                return;
            }

            SceneTreeTimer timer = tree.CreateTimer(0.2);
            timer.Timeout += () =>
            {
                try
                {
                    // ModSettingsUiContext is internal, so the navigation entry point is
                    // reached through reflection; failing to find it is not fatal.
                    System.Reflection.MethodInfo navigate = host?.GetType().GetMethod(
                        "NavigateToPage",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (navigate != null && host != null)
                    {
                        navigate.Invoke(host, new object[] { PageId });
                        ModLog.Info("page re-entered to rebuild the archive list");
                    }
                }
                catch (Exception ex)
                {
                    ModLog.Warn("rebuild navigation failed: " + ex.GetType().Name + ": " + ex.Message);
                }
            };
        }
        catch (Exception)
        {
        }
    }

    /// <summary>Rebuild the page so the status line and the archive list reflect what just happened.</summary>
    private static void Refresh(IModSettingsUiActionHost host)
    {
        try
        {
            ReloadArchives();
            host?.MarkDirty(_listRevision);
            host?.MarkDirty(_statusBinding);
            host?.RequestRefreshAfterDataModelBatchChange();
            host?.RequestRefresh();

            // Last resort: re-enter the page so RitsuLib rebuilds the entry list from
            // scratch. Deferred, because doing this inside the button callback would
            // tear down the control that is still handling the click.
            ScheduleRebuild(host);
        }
        catch (Exception ex)
        {
            ModLog.Warn("refresh failed: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    public static string CurrentStatus => _status;
}
