using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DynamicIsland.Core;

public sealed record CalculationResult(bool Success, string Display, double Value = 0)
{
    public static CalculationResult Fail(string message) => new(false, message);
}

/// <summary>
/// Kalkulator dan konversi satuan offline untuk command palette (Tahap 5a).
///
/// Mendukung aritmetika dasar, persen Indonesia ("15% dari 240 ribu"), skala
/// ribu/juta/miliar, dan konversi panjang, massa, waktu, data, serta suhu.
/// Tidak ada konversi kurs: itu butuh internet dan sengaja tidak disertakan.
/// </summary>
public static class Calculator
{
    private static readonly Dictionary<string, double> Length = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mm"] = 0.001, ["milimeter"] = 0.001,
        ["cm"] = 0.01, ["sentimeter"] = 0.01,
        ["m"] = 1, ["meter"] = 1,
        ["km"] = 1000, ["kilometer"] = 1000,
        ["inci"] = 0.0254, ["inch"] = 0.0254,
        ["kaki"] = 0.3048, ["feet"] = 0.3048, ["ft"] = 0.3048,
        ["yard"] = 0.9144,
        ["mil"] = 1609.344, ["mile"] = 1609.344,
    };

    private static readonly Dictionary<string, double> Mass = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mg"] = 0.001, ["miligram"] = 0.001,
        ["g"] = 1, ["gram"] = 1,
        ["ons"] = 100,
        ["kg"] = 1000, ["kilogram"] = 1000,
        ["kuintal"] = 100_000,
        ["ton"] = 1_000_000,
        ["pon"] = 453.59237, ["pound"] = 453.59237, ["lb"] = 453.59237,
    };

    private static readonly Dictionary<string, double> Time = new(StringComparer.OrdinalIgnoreCase)
    {
        ["detik"] = 1, ["sec"] = 1, ["second"] = 1, ["s"] = 1,
        ["menit"] = 60, ["minute"] = 60, ["min"] = 60,
        ["jam"] = 3600, ["hour"] = 3600,
        ["hari"] = 86_400, ["day"] = 86_400,
        ["minggu"] = 604_800, ["week"] = 604_800,
    };

    private static readonly Dictionary<string, double> Data = new(StringComparer.OrdinalIgnoreCase)
    {
        ["byte"] = 1, ["b"] = 1,
        ["kb"] = 1024, ["kilobyte"] = 1024,
        ["mb"] = 1024d * 1024, ["megabyte"] = 1024d * 1024,
        ["gb"] = 1024d * 1024 * 1024, ["gigabyte"] = 1024d * 1024 * 1024,
        ["tb"] = 1024d * 1024 * 1024 * 1024, ["terabyte"] = 1024d * 1024 * 1024 * 1024,
    };

    private static readonly Regex ScalePattern = new(
        @"(?<number>\d+(?:[.,]\d+)?)\s*(?<scale>ribu|rb|juta|jt|miliar|milyar|triliun)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PercentPattern = new(
        @"(?<number>\d+(?:[.,]\d+)?)\s*%",
        RegexOptions.Compiled);

    private static readonly Regex ThousandSeparator = new(@"\b\d{1,3}(?:\.\d{3})+\b", RegexOptions.Compiled);

    /// <summary>True bila input tampak seperti hitungan, bukan kata biasa.</summary>
    public static bool LooksLikeExpression(string? input) =>
        !string.IsNullOrWhiteSpace(input) &&
        (input.Any(char.IsDigit) || input.Contains('%')) &&
        input.Trim().Length <= 80;

    public static bool TryCalculate(string? input, out string display)
    {
        var result = Evaluate(input);
        display = result.Display;
        return result.Success;
    }

    public static CalculationResult Evaluate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return CalculationResult.Fail("Kosong");

        var text = input.Trim();

        var conversion = SplitConversion(text);
        if (conversion is { } pair)
        {
            var left = EvaluateExpression(pair.Left);
            if (!left.Success) return left;

            if (!TryConvert(left.Value, pair.From, pair.To, out var converted))
            {
                return CalculationResult.Fail($"Satuan tidak dikenal: {pair.To}");
            }

            return new CalculationResult(true, $"{FormatNumber(converted)} {pair.To}", converted);
        }

        return EvaluateExpression(text);
    }

    private static CalculationResult EvaluateExpression(string input)
    {
        try
        {
            var normalized = Normalize(input);
            var value = new Parser(normalized).Parse();
            return new CalculationResult(true, FormatNumber(value), value);
        }
        catch (FormatException error)
        {
            return CalculationResult.Fail(error.Message);
        }
        catch (DivideByZeroException)
        {
            return CalculationResult.Fail("Tidak bisa dibagi nol");
        }
    }

    /// <summary>Menerjemahkan kata Indonesia menjadi ekspresi aritmetika biasa.</summary>
    private static string Normalize(string input)
    {
        // Titik ribuan Indonesia dibuang dulu, baru koma desimal jadi titik.
        var text = ThousandSeparator.Replace(input, match => match.Value.Replace(".", string.Empty))
            .ToLowerInvariant()
            .Replace("×", "*")
            .Replace("x", "*")
            .Replace(":", "/")
            .Replace(",", ".");

        text = ScalePattern.Replace(text, match =>
        {
            var number = match.Groups["number"].Value;
            var scale = match.Groups["scale"].Value switch
            {
                "rb" => "1000",
                "jt" => "1000000",
                "milyar" => "1000000000",
                "ribu" => "1000",
                "juta" => "1000000",
                "miliar" => "1000000000",
                _ => "1000000000000",
            };

            return $"({number}*{scale})";
        });

        text = PercentPattern.Replace(text, match => $"({match.Groups["number"].Value}/100)");
        text = text.Replace("dari", "*");
        text = text.Replace("dikali", "*").Replace("dibagi", "/").Replace("ditambah", "+").Replace("dikurangi", "-");
        text = text.Replace(" ", string.Empty);

        return text;
    }

    private readonly record struct Conversion(string Left, string From, string To);

    private static Conversion? SplitConversion(string input)
    {
        var markers = new[] { " ke ", " to ", " dalam " };
        var lower = input.ToLowerInvariant();

        foreach (var marker in markers)
        {
            var index = lower.IndexOf(marker, StringComparison.Ordinal);
            if (index <= 0) continue;

            var left = input[..index].Trim();
            var right = input[(index + marker.Length)..].Trim();

            // Satuan tujuan harus satu kata tanpa angka.
            if (right.Length == 0 || right.Any(char.IsDigit) || right.Any(char.IsWhiteSpace)) continue;

            var parts = left.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !IsUnit(parts[^1])) continue;

            var unitFrom = parts[^1];
            var valuePart = string.Join(' ', parts[..^1]);
            return new Conversion(valuePart, unitFrom, right);
        }

        return null;
    }

    private static bool IsUnit(string name) =>
        Length.ContainsKey(name) || Mass.ContainsKey(name) || Time.ContainsKey(name) || Data.ContainsKey(name) || IsTemperature(name);

    private static bool IsTemperature(string name) =>
        name is "c" or "f" or "k" or "r" or "celsius" or "celcius" or "fahrenheit" or "kelvin" or "reamur" or "réaumur";

    public static bool TryConvert(double value, string from, string to, out double converted)
    {
        converted = 0;

        if (Length.TryGetValue(from, out var fromLength) && Length.TryGetValue(to, out var toLength))
        {
            converted = value * fromLength / toLength;
            return true;
        }

        if (Mass.TryGetValue(from, out var fromMass) && Mass.TryGetValue(to, out var toMass))
        {
            converted = value * fromMass / toMass;
            return true;
        }

        if (Time.TryGetValue(from, out var fromTime) && Time.TryGetValue(to, out var toTime))
        {
            converted = value * fromTime / toTime;
            return true;
        }

        if (Data.TryGetValue(from, out var fromData) && Data.TryGetValue(to, out var toData))
        {
            converted = value * fromData / toData;
            return true;
        }

        if (IsTemperature(from) && IsTemperature(to))
        {
            var celsius = ToCelsius(value, from);
            converted = FromCelsius(celsius, to);
            return true;
        }

        return false;
    }

    private static double ToCelsius(double value, string unit) => unit switch
    {
        "f" or "fahrenheit" => (value - 32) * 5 / 9,
        "k" or "kelvin" => value - 273.15,
        "r" or "reamur" or "réaumur" => value * 5 / 4,
        _ => value,
    };

    private static double FromCelsius(double value, string unit) => unit switch
    {
        "f" or "fahrenheit" => (value * 9 / 5) + 32,
        "k" or "kelvin" => value + 273.15,
        "r" or "reamur" or "réaumur" => value * 4 / 5,
        _ => value,
    };

    /// <summary>Format Indonesia: titik ribuan, koma desimal, maksimal dua desimal.</summary>
    public static string FormatNumber(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return "—";

        var rounded = Math.Round(value, 2);
        var text = rounded.ToString("0.##", CultureInfo.InvariantCulture);

        var negative = text.StartsWith('-');
        if (negative) text = text[1..];

        var parts = text.Split('.');
        var integer = parts[0];
        var grouped = new StringBuilder();

        for (var index = 0; index < integer.Length; index++)
        {
            if (index > 0 && (integer.Length - index) % 3 == 0) grouped.Append('.');
            grouped.Append(integer[index]);
        }

        var result = parts.Length > 1 ? $"{grouped},{parts[1]}" : grouped.ToString();
        return negative ? "-" + result : result;
    }

    private sealed class Parser
    {
        private readonly string text;
        private int index;

        public Parser(string text) => this.text = text;

        public double Parse()
        {
            var value = Expression();
            Skip();
            if (index < text.Length) throw new FormatException("Ekspresi tidak valid");
            return value;
        }

        private double Expression()
        {
            var value = Term();

            while (true)
            {
                Skip();
                if (Take('+')) value += Term();
                else if (Take('-')) value -= Term();
                else return value;
            }
        }

        private double Term()
        {
            var value = Factor();

            while (true)
            {
                Skip();
                if (Take('*')) value *= Factor();
                else if (Take('/'))
                {
                    var divisor = Factor();
                    if (divisor == 0) throw new DivideByZeroException();
                    value /= divisor;
                }
                else return value;
            }
        }

        private double Factor()
        {
            Skip();
            if (Take('+')) return Factor();
            if (Take('-')) return -Factor();

            if (Take('('))
            {
                var value = Expression();
                Skip();
                if (!Take(')')) throw new FormatException("Kurung tidak ditutup");
                return value;
            }

            return Number();
        }

        private double Number()
        {
            Skip();
            var start = index;
            var seenDot = false;

            while (index < text.Length)
            {
                var character = text[index];
                if (char.IsDigit(character))
                {
                    index++;
                    continue;
                }

                if (character == '.' && !seenDot)
                {
                    seenDot = true;
                    index++;
                    continue;
                }

                break;
            }

            if (index == start) throw new FormatException("Angka tidak ditemukan");

            return double.Parse(text[start..index], CultureInfo.InvariantCulture);
        }

        private bool Take(char character)
        {
            if (index >= text.Length || text[index] != character) return false;
            index++;
            return true;
        }

        private void Skip()
        {
            while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
        }
    }
}
