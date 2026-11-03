namespace DynamicIsland.Core;

/// <summary>
/// Antrean aktivitas berprioritas (lihat <c>shared/SPEC.md</c> bagian 3).
///
/// Aturan:
/// - satu aktivitas utama: prioritas tertinggi, seri dimenangkan yang lebih dulu masuk;
/// - <c>Upsert</c> dengan id yang sama menggantikan di tempat tanpa mengubah urutan FIFO;
/// - mencabut aktivitas utama mempromosikan aktivitas berikutnya;
/// - pengguna bisa mempromosikan aktivitas kedua lewat <see cref="Promote"/>;
/// - urutan aktivitas yang tidak berubah tidak boleh berubah.
/// </summary>
public sealed class ActivityQueue
{
    private readonly Dictionary<string, Activity> storage = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> insertionOrder = new(StringComparer.Ordinal);
    private long nextSequence;

    private string? promotedId;
    private int promotionCeiling;

    public int Count => storage.Count;

    public bool IsEmpty => storage.Count == 0;

    /// <summary>
    /// Aktivitas yang dipromosikan pengguna dan sedang memimpin antrean.
    /// Null berarti urutan murni mengikuti prioritas.
    /// </summary>
    public string? PromotedId => promotedId;

    /// <summary>Semua aktivitas diurutkan menurut prioritas lalu urutan masuk. Utama ada di indeks 0.</summary>
    public IReadOnlyList<Activity> Ordered
    {
        get
        {
            var ordered = storage.Values
                .OrderByDescending(activity => activity.Priority)
                .ThenBy(activity => insertionOrder[activity.Id])
                .ToList();

            // Promosi pengguna menimpa prioritas, tanpa mengubah nilai prioritasnya.
            if (promotedId is not null && storage.TryGetValue(promotedId, out var promoted))
            {
                ordered.RemoveAll(activity => activity.Id == promotedId);
                ordered.Insert(0, promoted);
            }

            return ordered;
        }
    }

    public Activity? Primary => Ordered is { Count: > 0 } ordered ? ordered[0] : null;

    /// <summary>Aktivitas kedua, calon kapsul minimal terpisah (Tahap 3).</summary>
    public Activity? Secondary => Ordered is { Count: > 1 } ordered ? ordered[1] : null;

    public Activity? Find(string id) => storage.GetValueOrDefault(id);

    public Activity? Upsert(Activity activity)
    {
        if (!insertionOrder.ContainsKey(activity.Id))
        {
            insertionOrder[activity.Id] = nextSequence;
            nextSequence++;
        }

        storage[activity.Id] = activity;
        DropPromotionForMoreImportant(activity);
        return activity;
    }

    /// <summary>
    /// Menjadikan satu aktivitas utama atas pilihan pengguna (kapsul kedua diketuk).
    /// Prioritas tidak diubah: urutan hanya ditimpa selama promosi berlaku.
    /// </summary>
    public bool Promote(string id)
    {
        if (!storage.ContainsKey(id)) return false;

        promotedId = id;
        promotionCeiling = storage.Values.Max(activity => activity.Priority);
        return true;
    }

    /// <summary>Mengakhiri promosi dan kembali ke urutan prioritas.</summary>
    public void ClearPromotion() => promotedId = null;

    public Activity? Remove(string id)
    {
        insertionOrder.Remove(id);
        if (promotedId == id) promotedId = null;
        return storage.Remove(id, out var removed) ? removed : null;
    }

    public void Clear()
    {
        storage.Clear();
        insertionOrder.Clear();
        promotedId = null;
    }

    /// <summary>Mengubah progres satu aktivitas. Null bila id tidak ada.</summary>
    public Activity? UpdateProgress(string id, double progress)
    {
        if (!storage.TryGetValue(id, out var activity)) return null;
        var updated = activity with { Progress = Math.Clamp(progress, 0, 1) };
        storage[id] = updated;
        return updated;
    }

    /// <summary>Mengubah state satu aktivitas. Null bila id tidak ada.</summary>
    public Activity? SetState(string id, ActivityState state)
    {
        if (!storage.TryGetValue(id, out var activity)) return null;
        var updated = activity with { State = state };
        storage[id] = updated;
        return updated;
    }

    /// <summary>
    /// Mencabut semua Transient Alert yang sudah lewat waktu kedaluwarsanya.
    /// Mengembalikan id yang dicabut, urut menurut urutan masuk.
    /// </summary>
    public IReadOnlyList<string> PurgeExpired(DateTimeOffset now)
    {
        var expired = storage.Values
            .Where(activity => !activity.IsLive && activity.ExpiresAt is { } expiresAt && expiresAt <= now)
            .OrderBy(activity => insertionOrder[activity.Id])
            .Select(activity => activity.Id)
            .ToList();

        foreach (var id in expired) Remove(id);
        return expired;
    }

    /// <summary>
    /// Promosi berakhir begitu ada aktivitas yang lebih penting dari seluruh isi
    /// antrean saat promosi dibuat, supaya event mendesak tidak tertahan di bawah.
    /// </summary>
    private void DropPromotionForMoreImportant(Activity activity)
    {
        if (promotedId is null || activity.Id == promotedId) return;
        if (activity.Priority > promotionCeiling) promotedId = null;
    }

    /// <summary>Waktu kedaluwarsa paling awal di antara Transient Alert, untuk penjadwalan.</summary>
    public DateTimeOffset? NextExpiry =>
        storage.Values
            .Where(activity => !activity.IsLive)
            .Select(activity => activity.ExpiresAt)
            .Min();
}
