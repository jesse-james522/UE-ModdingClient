namespace OrionLauncher.Services;

internal static class FileSize
{
    public static string Format(long bytes)
    {
        if (bytes < 1024L)
            return $"{bytes} B";
        if (bytes < 1024L * 1024)
            return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024)
            return $"{bytes / 1024.0 / 1024.0:F1} MB";
        return $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
    }
}
