namespace PettyCash.Infrastructure.Security;

public static class SecretFiles
{
    public static async Task WriteAsync(string path, string contents, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(temp, contents, cancellationToken).ConfigureAwait(false);
        TryRestrict(temp);
        File.Move(temp, path, overwrite: true);
        TryRestrict(path);
    }

    public static void TryRestrict(string path)
    {
        if (!OperatingSystem.IsWindows() && File.Exists(path))
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
