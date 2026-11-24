using System;
using MegaCrit.Sts2.Core.Modding;

namespace SaveShuttle;

[ModInitializer(nameof(Init))]
public static class Entry
{
    public static void Init()
    {
        ModLog.Info("starting " + typeof(Entry).Assembly.GetName().Version);
        try
        {
            SettingsPage.Register();
        }
        catch (Exception ex)
        {
            ModLog.Error("settings page registration failed: " + ex.GetType().Name + ": " + ex.Message);
        }

        DebugHook.Start();
    }
}
