using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Windows;
using DynamicIsland.Core;

namespace DynamicIsland.App;

/// <summary>
/// Asisten AI opsional (Tahap 5c.17). Kunci hanya ada di memori jendela ini,
/// tidak ditulis ke settings.json, dan tidak ada data yang dikirim sebelum
/// pengguna menekan Kirim.
/// </summary>
public partial class AssistantWindow : Window
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(45) };

    private readonly IslandText text;

    public AssistantWindow(IslandText text, string? endpoint, string? model)
    {
        InitializeComponent();

        this.text = text;

        Title = text.Get("assistant.title");
        PrivacyText.Text = text.Get("assistant.privacy");
        EndpointLabel.Text = text.Get("assistant.endpoint");
        ModelLabel.Text = text.Get("assistant.model");
        KeyLabel.Text = text.Get("assistant.key");
        QuestionLabel.Text = text.Get("assistant.question");
        SendButton.Content = text.Get("assistant.send");
        EndpointBox.Text = endpoint ?? string.Empty;
        ModelBox.Text = model ?? string.Empty;
        AnswerText.Text = text.Get("assistant.empty");

        Loaded += (_, _) => QuestionBox.Focus();
    }

    public string Endpoint => EndpointBox.Text.Trim();

    public string Model => ModelBox.Text.Trim();

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        var question = QuestionBox.Text.Trim();
        var endpoint = EndpointBox.Text.Trim();
        var key = KeyBox.Password;

        if (question.Length == 0)
        {
            StatusText.Text = text.Get("assistant.needQuestion");
            return;
        }

        if (!AssistantRules.ValidEndpoint(endpoint))
        {
            StatusText.Text = text.Get("assistant.badEndpoint");
            return;
        }

        if (key.Length == 0)
        {
            StatusText.Text = text.Get("assistant.needKey");
            return;
        }

        SendButton.IsEnabled = false;
        StatusText.Text = text.Get("assistant.sending");
        AnswerText.Text = string.Empty;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            request.Content = new StringContent(
                AssistantRules.BuildRequest(question, Model),
                Encoding.UTF8,
                "application/json");

            using var response = await Client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                StatusText.Text = text.Format("assistant.failed", (int)response.StatusCode);
                Log.Write($"asisten: HTTP {(int)response.StatusCode}");
                return;
            }

            AnswerText.Text = ReadAnswer(body) ?? text.Get("assistant.noAnswer");
            StatusText.Text = string.Empty;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException)
        {
            StatusText.Text = text.Get("assistant.offline");
            Log.Write("asisten: " + error.Message);
        }
        finally
        {
            SendButton.IsEnabled = true;
        }
    }

    /// <summary>Membaca format chat completions; format lain tidak ditebak.</summary>
    private static string? ReadAnswer(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            return null;
        }

        var first = choices[0];
        if (!first.TryGetProperty("message", out var message) ||
            !message.TryGetProperty("content", out var content) ||
            content.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return content.GetString();
    }
}
