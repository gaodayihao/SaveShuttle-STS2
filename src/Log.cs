using MegaCrit.Sts2.Core.Logging;

namespace SaveShuttle;

internal static class ModLog
{
    private static readonly Logger _log = new Logger("SaveShuttle", LogType.Generic);

    public static void Info(string message) => _log.Info(message, 1);

    public static void Warn(string message) => _log.Warn(message, 1);

    public static void Error(string message) => _log.Error(message, 1);
}
