namespace DynamicIsland.Core;

/// <summary>Model tampilan kapsul: satu aktivitas utama plus aktivitas kedua.</summary>
public sealed record IslandPresentation(
    Activity? Primary = null,
    Activity? Secondary = null,
    DisplayMode Mode = DisplayMode.Standar);

/// <summary>
/// Inti logika Dynamic Island. Bebas dari UI supaya bisa dites tanpa WPF.
///
/// Waktu masuk lewat <c>now</c> dan <see cref="Tick"/>, jadi tes tidak butuh timer.
/// </summary>
public sealed class IslandController
{
    private readonly Func<DateTimeOffset> now;
    private readonly Dictionary<string, int> basePriorities = new(StringComparer.Ordinal);
    private DateTimeOffset? expandedAt;

    public IslandController(Func<DateTimeOffset>? now = null, TimeSpan? autoCollapseDelay = null)
    {
        this.now = now ?? (() => DateTimeOffset.Now);
        AutoCollapseDelay = autoCollapseDelay ?? StateMachine.AutoCollapseDelay;
    }

    public ActivityQueue Queue { get; } = new();

    public TimeSpan AutoCollapseDelay { get; }

    public IslandPresentation Presentation { get; private set; } = new();

    private DisplayMode displayMode = DisplayMode.Standar;

    /// <summary>Mode tampilan: standar (ukuran spec) atau ringkas (jejak lebih kecil).</summary>
    public DisplayMode DisplayMode
    {
        get => displayMode;
        set
        {
            if (displayMode == value) return;
            displayMode = value;
            Publish();
        }
    }

    private bool showsSecondary;

    /// <summary>Hanya tampilkan kapsul kedua bila fitur dua kapsul aktif (Tahap 3).</summary>
    public bool ShowsSecondary
    {
        get => showsSecondary;
        set
        {
            if (showsSecondary == value) return;
            showsSecondary = value;
            Publish();
        }
    }

    private PriorityPreset priorityPreset = PriorityPreset.Bawaan;

    /// <summary>
    /// Preset prioritas pilihan pengguna (Tahap 3). Bawaan tidak mengubah apa pun;
    /// preset lain menaikkan jenis yang dipilih ke <see cref="PriorityRules.BoostedPriority"/>.
    /// </summary>
    public PriorityPreset PriorityPreset
    {
        get => priorityPreset;
        set
        {
            if (priorityPreset == value) return;
            priorityPreset = value;
            ReapplyPriorities();
            Publish();
        }
    }

    /// <summary>Kapan expanded akan ciut sendiri. Null bila tidak ada yang expanded.</summary>
    public DateTimeOffset? AutoCollapseAt => expandedAt is { } start ? start + AutoCollapseDelay : null;

    /// <summary>Dipanggil setiap tampilan berubah.</summary>
    public event Action<IslandPresentation>? Changed;

    /// <summary>Pesan untuk event yang diabaikan. Null berarti dibuang.</summary>
    public Action<string>? OnError { get; set; }

    /// <summary>Membaca satu baris JSON. Baris tidak valid dilaporkan ke <see cref="OnError"/>.</summary>
    public void HandleLine(string line)
    {
        try
        {
            Handle(IslandEvent.Parse(line, now()));
        }
        catch (IslandEventException error)
        {
            OnError?.Invoke(error.Message);
        }
    }

    public void Handle(IslandEvent islandEvent)
    {
        switch (islandEvent)
        {
            case IslandEvent.Upsert upsert:
                Upsert(upsert.Activity with { State = StateMachine.InitialState });
                break;
            case IslandEvent.Progress progress:
                Queue.UpdateProgress(progress.Id, progress.Value);
                break;
            case IslandEvent.Expire expire:
                Remove(expire.Id);
                break;
            case IslandEvent.Clear clear:
                Remove(clear.Id);
                break;
            case IslandEvent.ClearAll:
                basePriorities.Clear();
                Queue.Clear();
                break;
        }

        Publish();
    }

    /// <summary>
    /// Memperbarui konten aktivitas yang sudah tampil tanpa mereset state-nya
    /// (dipakai monitor baterai, kalender, dan perangkat yang mengirim data berkala).
    /// Bila id belum ada, perilakunya sama dengan Upsert: masuk sebagai compact.
    /// </summary>
    public void Refresh(Activity activity)
    {
        var existing = Queue.Find(activity.Id);
        var next = existing is null
            ? activity with { State = StateMachine.InitialState }
            : activity with { State = existing.State };

        Upsert(next);
        Publish();
    }

    /// <summary>Menghapus satu aktivitas (dipakai monitor bila datanya hilang).</summary>
    public void Remove(string id)
    {
        basePriorities.Remove(id);
        Queue.Remove(id);
        Publish();
    }

    /// <summary>Dipanggil berkala oleh lapisan UI (mis. setiap 100 ms).</summary>
    public void Tick()
    {
        var current = now();
        foreach (var id in Queue.PurgeExpired(current)) basePriorities.Remove(id);

        if (Queue.Primary is { State: ActivityState.Expanded } primary
            && expandedAt is { } start
            && current >= start + AutoCollapseDelay)
        {
            Queue.SetState(primary.Id, ActivityState.Compact);
            expandedAt = null;
        }

        Publish();
    }

    /// <summary>Klik/ketuk pada kapsul utama: membuka satu langkah.</summary>
    public void TapPrimary()
    {
        if (Queue.Primary is not { } primary) return;
        if (StateMachine.Next(primary.State, StateTransition.Open) is not { } next) return;

        Queue.SetState(primary.Id, next);
        Publish();
    }

    /// <summary>
    /// Klik kapsul kedua: aktivitas itu naik jadi utama dan tampil compact.
    /// Hanya berlaku saat fitur dua kapsul aktif, karena kapsulnya harus terlihat.
    /// </summary>
    public bool PromoteSecondary()
    {
        if (!ShowsSecondary) return false;
        if (Queue.Secondary is not { } secondary) return false;

        Queue.Promote(secondary.Id);
        Queue.SetState(secondary.Id, ActivityState.Compact);
        Publish();
        return true;
    }

    /// <summary>Klik kedua atau gesture tutup: menutup satu langkah.</summary>
    public void ClosePrimary()
    {
        if (Queue.Primary is not { } primary) return;
        if (StateMachine.Next(primary.State, StateTransition.Close) is not { } next) return;

        Queue.SetState(primary.Id, next);
        Publish();
    }

    /// <summary>Mengembalikan urutan ke prioritas biasa (mis. kapsul kedua diketuk lagi tidak berguna).</summary>
    public void ClearPromotion()
    {
        Queue.ClearPromotion();
        Publish();
    }

    /// <summary>
    /// Mengulang hitungan ciut otomatis tanpa mengubah state. Dipakai tombol di
    /// dalam kartu (mis. kontrol media), supaya kartu tidak hilang saat dipakai.
    /// </summary>
    public void KeepExpanded()
    {
        if (Queue.Primary is { State: ActivityState.Expanded }) expandedAt = now();
    }

    /// <summary>Menurunkan expanded ke compact seolah timer ciut otomatis habis.</summary>
    public void AutoCollapseNow()
    {
        if (Queue.Primary is not { State: ActivityState.Expanded } primary) return;

        Queue.SetState(primary.Id, ActivityState.Compact);
        Publish();
    }

    /// <summary>
    /// Menyimpan prioritas asli aktivitas, lalu menerapkan preset di atasnya.
    /// Prioritas asli disimpan terpisah supaya preset bisa dimatikan lagi tanpa
    /// meninggalkan nilai yang sudah dinaikkan.
    /// </summary>
    private void Upsert(Activity activity)
    {
        if (priorityPreset == PriorityPreset.Bawaan)
        {
            basePriorities[activity.Id] = activity.Priority;
            Queue.Upsert(activity);
            return;
        }

        if (!basePriorities.TryGetValue(activity.Id, out var basePriority))
        {
            basePriority = activity.Priority;
            basePriorities[activity.Id] = basePriority;
        }

        Queue.Upsert(activity with
        {
            Priority = PriorityRules.PriorityFor(activity.Kind, basePriority, priorityPreset),
        });
    }

    /// <summary>Menyusun ulang prioritas isi antrean setelah preset berubah.</summary>
    private void ReapplyPriorities()
    {
        foreach (var activity in Queue.Ordered.ToList())
        {
            var basePriority = basePriorities.TryGetValue(activity.Id, out var known) ? known : activity.Priority;

            Queue.Upsert(activity with
            {
                Priority = PriorityRules.PriorityFor(activity.Kind, basePriority, priorityPreset),
            });
        }
    }

    private void Publish()
    {
        SyncExpandedClock();

        var next = new IslandPresentation(
            Primary: Queue.Primary,
            Secondary: ShowsSecondary ? Queue.Secondary : null,
            Mode: displayMode);

        if (next == Presentation) return;

        Presentation = next;
        Changed?.Invoke(next);
    }

    /// <summary>Timer ciut otomatis mulai saat expanded muncul, berhenti saat tidak expanded.</summary>
    private void SyncExpandedClock()
    {
        if (Queue.Primary is { State: ActivityState.Expanded })
        {
            expandedAt ??= now();
        }
        else
        {
            expandedAt = null;
        }
    }
}
