using System.IO;

namespace AIM.Desktop.Wpf;

internal static class AvatarAssetResolver
{
    public static string? Resolve(string? avatarImagePath)
    {
        if (string.IsNullOrWhiteSpace(avatarImagePath))
        {
            return null;
        }

        var path = avatarImagePath.Trim();
        var resolved = Path.IsPathRooted(path)
            ? path
            : Path.Combine(AppContext.BaseDirectory, path);

        return File.Exists(resolved) ? resolved : null;
    }
}
