namespace Aria.Cli.Services;

/// <summary>
/// Helper methods for path manipulation.
/// </summary>
internal static class PathHelper
{
    /// <summary>
    /// Expands tilde (~) in paths to the user's home directory.
    /// </summary>
    /// <param name="path">The path to expand.</param>
    /// <returns>The expanded path, or the original path if no expansion is needed.</returns>
    /// <exception cref="InvalidOperationException">Thrown when tilde expansion is needed but home directory cannot be determined.</exception>
    public static string ExpandTildePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path ?? string.Empty;

        if (path.StartsWith("~/") || path == "~")
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(home))
                home = Environment.GetEnvironmentVariable("HOME") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(home))
                throw new InvalidOperationException("Cannot expand tilde path: home directory could not be determined.");

            return path == "~" ? home : Path.Combine(home, path[2..]);
        }

        return path;
    }
}
