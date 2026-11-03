# Format Event Palsu (untuk uji manual)

Satu baris JSON per event. Dipakai oleh `windows/Scripts/fake-events.ps1`
untuk uji manual.

```
{"op":"upsert","activity":{"id":"track-1","kind":"media","title":"Lagu A","subtitle":"Artis B","progress":0.42,"isLive":true}}
{"op":"progress","id":"track-1","progress":0.55}
{"op":"expire","id":"track-1"}
{"op":"clear"}
{"op":"clearAll"}
```

## Field

| Field      | Wajib | Keterangan                                                        |
|------------|-------|-------------------------------------------------------------------|
| `op`       | ya    | `upsert`, `progress`, `expire`, `clear`, `clearAll`               |
| `id`       | ya*   | wajib untuk semua op kecuali `upsert` dan `clearAll`              |
| `kind`     | tidak | salah satu Kind; bawaan `notification`                            |
| `priority` | tidak | 0..100; bila kosong pakai prioritas bawaan Kind                   |
| `title`    | tidak | bawaan `""`                                                       |
| `subtitle` | tidak | opsional                                                          |
| `progress` | tidak | 0.0..1.0                                                          |
| `isLive`   | tidak | bawaan `true` untuk kind Live, `false` untuk Transient            |
| `ttlMs`    | tidak | umur Transient Alert dalam milidetik; bawaan 4000                 |
| `actions`  | tidak | array `{"id":"...","label":"...","glyph":"..."}`                  |

## Skenario uji minimal

1. `media` masuk -> kapsul compact dengan judul dan progres.
2. `timer` masuk saat media tampil -> timer jadi utama (prioritas 80 > 70).
3. `notification` masuk -> tampil 4 detik lalu hilang, timer tetap utama.
4. Dua `call` masuk -> yang lebih dulu tetap utama (FIFO pada prioritas sama).
5. `clear` pada aktivitas utama -> aktivitas berikutnya naik jadi utama.
