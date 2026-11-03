using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using DynamicIsland.Core;

namespace DynamicIsland.App;

/// <summary>
/// SDK plugin (Tahap 5c.18). Plugin hanya dimuat dari folder pengguna, hanya
/// bila saklar menyala, dan hanya izin yang dikenal yang diterima. Plugin
/// berjalan di proses ini; tidak ada toko tema atau unduhan otomatis.
/// </summary>
public sealed class PluginHost : IDisposable
{
    private readonly IslandController controller;
    private readonly List<IIslandPlugin> running = new();
    private readonly List<AssemblyLoadContext> contexts = new();

    public PluginHost(IslandController controller) => this.controller = controller;

    public IReadOnlyList<PluginManifest> Loaded { get; private set; } = Array.Empty<PluginManifest>();

    public static string DefaultFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DynamicIsland",
        "plugins");

    public void Load(bool enabled, string? folder = null)
    {
        Unload();

        if (!enabled) return;

        var root = folder ?? DefaultFolder;
        var manifests = PluginCatalog.Discover(root);

        foreach (var manifest in manifests)
        {
            var directory = FindDirectory(root, manifest);
            if (directory is null) continue;

            var entry = Path.Combine(directory, manifest.EntryPoint);
            if (!File.Exists(entry)) continue;

            try
            {
                var context = new AssemblyLoadContext("island-plugin-" + manifest.Id, isCollectible: true);
                var assembly = context.LoadFromAssemblyPath(entry);
                var type = assembly.GetTypes().FirstOrDefault(candidate =>
                    typeof(IIslandPlugin).IsAssignableFrom(candidate) && !candidate.IsAbstract);

                if (type is null || Activator.CreateInstance(type) is not IIslandPlugin plugin) continue;

                plugin.Start(new Context(controller));
                running.Add(plugin);
                contexts.Add(context);
                Log.Write($"plugin: {manifest.Name} {manifest.Version} dimuat");
            }
            catch (Exception error) when (error is BadImageFormatException or FileLoadException or TargetInvocationException or InvalidOperationException)
            {
                Log.Write($"plugin: {manifest.Id} gagal dimuat ({error.Message})");
            }
        }

        Loaded = manifests;
    }

    public void Unload()
    {
        foreach (var plugin in running)
        {
            try
            {
                plugin.Shutdown();
            }
            catch (Exception error) when (error is InvalidOperationException or NotSupportedException)
            {
                Log.Write("plugin: gagal berhenti (" + error.Message + ")");
            }
        }

        running.Clear();

        foreach (var context in contexts) context.Unload();
        contexts.Clear();
        Loaded = Array.Empty<PluginManifest>();
    }

    public void Dispose() => Unload();

    private static string? FindDirectory(string root, PluginManifest manifest)
    {
        foreach (var file in Directory.EnumerateFiles(root, PluginCatalog.ManifestName, SearchOption.AllDirectories))
        {
            var directory = Path.GetDirectoryName(file);
            if (directory is null) continue;

            var parsed = PluginCatalog.Parse(File.ReadAllText(file));
            if (parsed is not null && parsed.Id == manifest.Id) return directory;
        }

        return null;
    }

    private sealed class Context : IPluginContext
    {
        private readonly IslandController controller;

        public Context(IslandController controller) => this.controller = controller;

        public void Show(string title, string? subtitle = null)
        {
            controller.Refresh(new Activity(
                id: "plugin-activity",
                kind: ActivityKind.Device,
                priority: 18,
                title: title,
                subtitle: subtitle,
                isLive: false,
                expiresAt: DateTimeOffset.Now.AddSeconds(6)));
        }
    }
}
