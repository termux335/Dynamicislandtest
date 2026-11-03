using DynamicIsland.Core;

namespace DynamicIsland.App;

internal static class DemoEvents
{
    /// <summary>Dipakai oleh <c>--demo</c> supaya kapsul langsung terisi tanpa skrip.</summary>
    public static void Seed(IslandController controller)
    {
        controller.HandleLine("""
            {"op":"upsert","activity":{"id":"track-1","kind":"media","title":"Midnight City","subtitle":"M83","progress":0.32,
            "actions":[{"id":"playpause","label":"Jeda","glyph":"pause"},{"id":"next","label":"Lagu berikutnya","glyph":"next"}]}}
            """);

        controller.HandleLine("""
            {"op":"upsert","activity":{"id":"timer-1","kind":"timer","title":"Fokus 24:38","subtitle":"Sesi 2 dari 4","progress":0.18,
            "actions":[{"id":"pause","label":"Jeda timer","glyph":"pause"}]}}
            """);

        controller.HandleLine("""
            {"op":"upsert","activity":{"id":"notif-1","kind":"notification","title":"Rapat mulai 5 menit lagi","subtitle":"Tim Desain","ttlMs":8000,
            "actions":[{"id":"reply","label":"Balas","glyph":"reply"}]}}
            """);

        Log.Write("demo dijalankan");
    }
}
