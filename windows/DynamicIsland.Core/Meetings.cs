namespace DynamicIsland.Core;

/// <summary>
/// Satu aplikasi yang sedang memakai mikrofon, dari registry privasi Windows.
/// <paramref name="App"/> adalah nama pendek untuk ditampilkan (mis. "Teams").
/// </summary>
public sealed record MicrophoneUser(string App, DateTimeOffset StartedAt);

/// <summary>
/// Aturan aktivitas rapat/panggilan (Tahap 1): tampil selama mikrofon dipakai
/// aplikasi lain, lengkap dengan durasi, tombol bisukan, dan tombol kembali.
///
/// Pembacaan registry ada di lapisan App; bagian ini murni logika supaya bisa
/// dites tanpa Windows.
/// </summary>
public static class MeetingPlan
{
    public const string ActivityId = "meeting";

    /// <summary>
    /// Aktivitas untuk pemakai mikrofon saat ini, atau null bila tidak ada.
    /// Aplikasi yang paling dulu menyalakan mikrofon dipakai sebagai judul.
    /// </summary>
    public static Activity? ActivityFor(
        IReadOnlyList<MicrophoneUser> users,
        DateTimeOffset now,
        bool muted,
        IslandText? text = null)
    {
        if (users.Count == 0) return null;

        var strings = text ?? IslandText.Default;
        var first = users.OrderBy(user => user.StartedAt).First();

        var elapsed = now - first.StartedAt;
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
        var duration = TimerEngine.Format(elapsed);

        var title = string.IsNullOrWhiteSpace(first.App) ? strings.Get("meeting.title") : first.App;
        var subtitle = muted
            ? strings.Format("meeting.subtitleMuted", duration)
            : strings.Format("meeting.subtitle", duration);

        return new Activity(
            id: ActivityId,
            kind: ActivityKind.Call,
            title: title,
            subtitle: subtitle,
            isLive: true,
            actions: new[]
            {
                new ActivityAction(
                    "meeting.mic-toggle",
                    strings.Get(muted ? "meeting.unmute" : "meeting.mute"),
                    muted ? "unmute" : "mute"),
                new ActivityAction("meeting.back", strings.Get("meeting.back"), "back"),
            });
    }

    /// <summary>
    /// Nama pendek dari kunci registry privasi. Kunci NonPackaged berbentuk path
    /// dengan '#' sebagai pemisah, mis. <c>C:#Program Files#Microsoft Teams#current#Teams.exe</c>.
    /// </summary>
    public static string AppNameFromConsentKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;

        var normalized = key.Replace('#', System.IO.Path.DirectorySeparatorChar).TrimEnd(
            System.IO.Path.DirectorySeparatorChar,
            '/');

        var name = System.IO.Path.GetFileName(normalized);
        if (string.IsNullOrEmpty(name)) name = normalized;
        if (name.Length == 0) return string.Empty;

        var withoutExtension = System.IO.Path.GetFileNameWithoutExtension(name);
        return string.IsNullOrWhiteSpace(withoutExtension) ? name : withoutExtension;
    }
}
