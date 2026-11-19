using System.Reflection;

namespace BrightTray;

/// <summary>Derlemeye gömülü sürüm bilgisi. Değer BrightTray.csproj içindeki &lt;Version&gt; alanından gelir.</summary>
public static class AppInfo
{
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            // "1.2.3+abcdef" gibi bir ek varsa at.
            int plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
