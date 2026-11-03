namespace DynamicIsland.Core;

/// <summary>Pengingat kebiasaan (Tahap 4, Mode Kocak). Selalu bisa dimatikan.</summary>
public enum HabitKind
{
    Drink,
    EyeRest,
    ScreenTime,
}

public sealed record HabitSettings(
    bool Enabled = false,
    int DrinkMinutes = 45,
    int EyeMinutes = 20,
    int ScreenTimeMinutes = 120);

public sealed record HabitReminder(HabitKind Kind, string Title, string Subtitle, DateTimeOffset DueAt);

/// <summary>
/// Pengingat minum, istirahat mata (20-20-20), dan lama di depan layar.
///
/// Waktu masuk lewat <see cref="Tick"/> sehingga tes tidak butuh timer. Hanya
/// menghitung waktu; tidak membaca aktivitas atau isi layar.
/// </summary>
public sealed class HabitCoach
{
    private readonly Func<DateTimeOffset> now;
    private readonly IslandText text;
    private readonly Dictionary<HabitKind, DateTimeOffset> due = new();
    private HabitSettings settings;
    private bool started;

    public HabitCoach(HabitSettings? settings = null, Func<DateTimeOffset>? now = null, IslandText? text = null)
    {
        this.settings = settings ?? new HabitSettings();
        this.now = now ?? (() => DateTimeOffset.Now);
        this.text = text ?? IslandText.Default;
    }

    public HabitSettings Settings => settings;

    /// <summary>Mengganti preferensi; jadwal dihitung ulang dari sekarang.</summary>
    public void Configure(HabitSettings settings)
    {
        this.settings = settings;
        Reset();
    }

    public void Reset()
    {
        started = false;
        due.Clear();
    }

    /// <summary>Pengingat yang jatuh tempo, atau null. Maksimal satu per panggilan.</summary>
    public HabitReminder? Tick()
    {
        if (!settings.Enabled) return null;

        var at = now();

        if (!started)
        {
            started = true;
            foreach (var kind in Enum.GetValues<HabitKind>()) due[kind] = at.AddMinutes(Interval(kind));
            return null;
        }

        foreach (var kind in Enum.GetValues<HabitKind>())
        {
            if (due[kind] > at) continue;

            due[kind] = at.AddMinutes(Interval(kind));
            return Build(kind, at);
        }

        return null;
    }

    /// <summary>Waktu sampai pengingat berikutnya, untuk log/diagnostik.</summary>
    public TimeSpan? NextIn()
    {
        if (!settings.Enabled || !started || due.Count == 0) return null;

        var next = due.Values.Min();
        var left = next - now();
        return left < TimeSpan.Zero ? TimeSpan.Zero : left;
    }

    private int Interval(HabitKind kind) => kind switch
    {
        HabitKind.Drink => Math.Max(1, settings.DrinkMinutes),
        HabitKind.EyeRest => Math.Max(1, settings.EyeMinutes),
        _ => Math.Max(1, settings.ScreenTimeMinutes),
    };

    private HabitReminder Build(HabitKind kind, DateTimeOffset at)
    {
        var key = kind switch
        {
            HabitKind.Drink => "habit.drink",
            HabitKind.EyeRest => "habit.eye",
            _ => "habit.screen",
        };

        return new HabitReminder(kind, text.Get(key + ".title"), text.Get(key + ".subtitle"), at);
    }

    /// <summary>Aktivitas Transient Alert dari pengingat.</summary>
    public static Activity ActivityFor(HabitReminder reminder, DateTimeOffset now, IslandText? text = null)
    {
        var strings = text ?? IslandText.Default;

        return new Activity(
            id: "habit-" + reminder.Kind.ToString().ToLowerInvariant(),
            kind: ActivityKind.Notification,
            priority: 25,
            title: reminder.Title,
            subtitle: reminder.Subtitle,
            isLive: false,
            expiresAt: now.AddSeconds(8),
            actions: new[] { new ActivityAction("habit.dismiss", strings.Get("habit.dismiss"), "check") });
    }
}
