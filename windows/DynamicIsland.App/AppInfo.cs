using System.Reflection;

namespace DynamicIsland.App;

/// <summary>
/// Versi build yang sedang jalan, dibaca dari metadata assembly supaya angka di
/// jendela pengaturan dan log tidak pernah berbeda dari yang dikompilasi.
/// </summary>
internal static class AppInfo
{
    public static string Version { get; } = Read();

    private static string Read()
    {
        var assembly = typeof(AppInfo).Assembly;

        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            // SDK menempelkan hash commit setelah "+"; tidak perlu di UI.
            var plus = informational.IndexOf('+');
            return plus > 0 ? informational[..plus] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
