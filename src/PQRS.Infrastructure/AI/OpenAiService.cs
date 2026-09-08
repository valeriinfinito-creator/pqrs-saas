using System.ClientModel;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using Pgvector;
using PQRS.Application.Common.Interfaces;
using Microsoft.Extensions.Http;

namespace PQRS.Infrastructure.AI;

public class GroqAiService : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly ChatClient _chatClient;
    private readonly string _embeddingModel;

    public GroqAiService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        var groqApiKey = configuration["Groq:ApiKey"]
            ?? throw new InvalidOperationException("Falta configurar 'Groq:ApiKey' (user-secrets).");

        var chatModel = configuration["Groq:ChatModel"] ?? "gpt-5.6-terra";
        var ollamaBaseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
        _embeddingModel = configuration["Ollama:EmbeddingModel"] ?? "all-minilm";

        var groqOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri("https://api.groq.com/openai/v1")
        };
        _chatClient = new ChatClient(chatModel, new ApiKeyCredential(groqApiKey), groqOptions);

        _httpClient = httpClientFactory.CreateClient("Ollama");
        _httpClient.BaseAddress = new Uri(ollamaBaseUrl);
    }

    public async Task<Vector> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var requestBody = new { model = _embeddingModel, input = text };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/api/embed", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseJson);

        var embeddingArray = doc.RootElement.GetProperty("embeddings")[0];
        var floats = embeddingArray.EnumerateArray().Select(x => x.GetSingle()).ToArray();

        return new Vector(floats);
    }

    public async Task<TicketTriageResult> ClassifyTicketAsync(string subject, string description, CancellationToken cancellationToken = default)
    {
        var systemPrompt = """
        Eres un clasificador de tickets de soporte al cliente (PQRS). Analiza el
        asunto y la descripción, y responde ÚNICAMENTE con un objeto JSON válido,
        sin texto adicional, sin markdown, con exactamente esta forma:

        {
          "type": "Peticion" | "Queja" | "Reclamo" | "Sugerencia",
          "priority": "Baja" | "Media" | "Alta",
          "sentiment": "Positivo" | "Neutro" | "Negativo",
          "summary": "resumen ejecutivo de 1 a 2 oraciones en español"
        }

        Reglas:
        - Queja o Reclamo con tono negativo severo => priority "Alta".
        - Peticion o Sugerencia estándar => priority "Baja" o "Media".
        - Usa exactamente esos valores en inglés/español indicados, sin variaciones.
        """;

        var userPrompt = $"Asunto: {subject}\nDescripción: {description}";

        var messages = new List<ChatMessage>
    {
        new SystemChatMessage(systemPrompt),
        new UserChatMessage(userPrompt)
    };

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        var result = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);
        var json = result.Value.Content[0].Text;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new TicketTriageResult(
            Type: root.GetProperty("type").GetString() ?? "Peticion",
            Priority: root.GetProperty("priority").GetString() ?? "Media",
            Sentiment: root.GetProperty("sentiment").GetString() ?? "Neutro",
            Summary: root.GetProperty("summary").GetString() ?? "");
    }
    public async Task<string> GenerateAnswerAsync(string question, IEnumerable<string> contextArticles, CancellationToken cancellationToken = default)
    {
        var context = string.Join("\n\n---\n\n", contextArticles);

        var systemPrompt = $"""
            Eres un asistente de soporte al cliente. Responde ÚNICAMENTE basándote en el
            siguiente contexto de la base de conocimiento de la empresa. Si el contexto
            no contiene información suficiente para responder, dilo explícitamente y
            no inventes información. Sé breve y directo.

            Contexto:
            {context}
            """;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(question)
        };

        var result = await _chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);

        return result.Value.Content[0].Text;
    }
}