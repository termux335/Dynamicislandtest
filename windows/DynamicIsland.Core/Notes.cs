using System.Text.Json;
using System.Text.Json.Serialization;

namespace DynamicIsland.Core;

public enum NoteKind
{
    Note,
    Snippet,
}

/// <summary>Satu catatan atau potongan teks yang sering dipakai.</summary>
public sealed record Note(
    string Id,
    string Text,
    NoteKind Kind,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RemindAt = null);

/// <summary>
/// Catatan dan snippet lokal (Tahap 5b). Tersimpan sebagai JSON di perangkat,
/// tanpa sinkronisasi dan tanpa jaringan.
/// </summary>
public sealed class NoteStore
{
    private const int MaxNotes = 100;
    private const int MaxTextLength = 4000;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly List<Note> notes = new();

    public NoteStore(string? path = null) => FilePath = path ?? DefaultPath;

    public static string DefaultPath => Environment.GetEnvironmentVariable("ISLAND_NOTES") is { Length: > 0 } custom
        ? custom
        : System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DynamicIsland",
            "notes.json");

    public string FilePath { get; }

    public IReadOnlyList<Note> All => notes;

    /// <summary>Menambah catatan; teks kosong diabaikan. Mengembalikan entri baru.</summary>
    public Note? Add(string? text, NoteKind kind = NoteKind.Note, DateTimeOffset? at = null, DateTimeOffset? remindAt = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var value = text.Trim();
        if (value.Length > MaxTextLength) value = value[..MaxTextLength];

        var createdAt = at ?? DateTimeOffset.Now;

        // Teks yang sama dipindah ke depan, bukan diduplikasi.
        notes.RemoveAll(note => note.Text == value && note.Kind == kind);

        var item = new Note(
            Guid.NewGuid().ToString("N")[..10],
            value,
            kind,
            createdAt,
            remindAt);

        notes.Insert(0, item);

        if (notes.Count > MaxNotes) notes.RemoveRange(MaxNotes, notes.Count - MaxNotes);

        return item;
    }

    public bool Remove(string id) => notes.RemoveAll(note => note.Id == id) > 0;

    public IReadOnlyList<Note> Search(string? query, int limit = 8)
    {
        if (string.IsNullOrWhiteSpace(query)) return notes.Take(limit).ToList();

        return notes
            .Where(note => note.Text.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .ToList();
    }

    public IReadOnlyList<Note> Due(DateTimeOffset now) =>
        notes.Where(note => note.RemindAt is { } at && at <= now).ToList();

    public bool Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return false;

            var loaded = JsonSerializer.Deserialize<List<Note>>(File.ReadAllText(FilePath), Options);
            if (loaded is null) return false;

            notes.Clear();
            notes.AddRange(loaded.Where(note => !string.IsNullOrWhiteSpace(note.Text)).Take(MaxNotes));
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    public bool Save()
    {
        try
        {
            var folder = System.IO.Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            File.WriteAllText(FilePath, JsonSerializer.Serialize(notes, Options));
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
