using Pgvector;

namespace PQRS.Application.Common.Interfaces;

public interface IAiService
{
    Task<Vector> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    Task<string> GenerateAnswerAsync(string question, IEnumerable<string> contextArticles, CancellationToken cancellationToken = default);

    //Analiza el texto de un ticket y devuelve su clasificación automática
    Task<TicketTriageResult> ClassifyTicketAsync(string subject, string description, CancellationToken cancellationToken = default);

    
}

//Resultado estructurado del análisis de triaje de un ticket
public record TicketTriageResult(string Type, string Priority, string Sentiment, string Summary);

