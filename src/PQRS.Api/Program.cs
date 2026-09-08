using Microsoft.EntityFrameworkCore;
using PQRS.Infrastructure.Persistence;
using PQRS.Application.Tenants;
using PQRS.Domain.Entities;
using PQRS.Api.Middleware;
using PQRS.Application.Tickets;
using PQRS.Domain.Enums;
using PQRS.Application.Auth;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using PQRS.Application.KnowledgeBase;
using PQRS.Application.Common.Interfaces;
using PQRS.Infrastructure.AI;
using Pgvector.EntityFrameworkCore;
using PQRS.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Documentación OpenAPI 
builder.Services.AddOpenApi();

//  Base de datos 
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'DefaultConnection'.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));

builder.Services.AddHttpClient();
builder.Services.AddSingleton<IAiService, GroqAiService>();

// Autenticación JWT 
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtSecretKey = jwtSection["SecretKey"]!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSignalR();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseMiddleware<DynamicCorsMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<WidgetTenantResolutionMiddleware>();
app.MapHub<TicketNotificationHub>("/hubs/tickets");

// confirma que la API arrancó y que puede hablar con la base de datos.
app.MapGet("/health", async (ApplicationDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();
    return Results.Ok(new { status = "ok", database = canConnect ? "connected" : "unreachable" });
});

// Endpoints de Tenants (solo para setup/administración)
app.MapPost("/api/v1/tenants", async (CreateTenantRequest request, ApplicationDbContext db) =>
{
    var tenant = new Tenant
    {
        Name = request.Name,
        AllowedDomain = request.AllowedDomain
    };

    db.Tenants.Add(tenant);
    await db.SaveChangesAsync();

    var response = new TenantResponse(
        tenant.Id,
        tenant.Name,
        tenant.AllowedDomain,
        tenant.WidgetApiKey,
        tenant.IsActive);

    return Results.Created($"/api/v1/tenants/{tenant.Id}", response);
});

// Endpoints públicos del Widget 
app.MapPost("/api/v1/widget/tickets", async (CreateTicketRequest request, HttpContext context, ApplicationDbContext db, IAiService ai, IHubContext<TicketNotificationHub> hub) =>
{
    var tenantId = (Guid)context.Items["TenantId"]!;

    var ticket = new Ticket
    {
        TenantId = tenantId,
        CustomerName = request.CustomerName,
        CustomerEmail = request.CustomerEmail,
        Subject = request.Subject,
        Description = request.Description,
        OriginatedFromRag = request.OriginatedFromRag
    };

    // Triaje automático: clasificamos el ticket con IA antes de guardarlo.
    try
    {
        var triage = await ai.ClassifyTicketAsync(request.Subject, request.Description);

        if (Enum.TryParse<TicketType>(triage.Type, ignoreCase: true, out var parsedType))
            ticket.Type = parsedType;

        if (Enum.TryParse<TicketPriority>(triage.Priority, ignoreCase: true, out var parsedPriority))
            ticket.Priority = parsedPriority;

        if (Enum.TryParse<Sentiment>(triage.Sentiment, ignoreCase: true, out var parsedSentiment))
            ticket.Sentiment = parsedSentiment;

        ticket.AiSummary = triage.Summary;
    }
    catch (Exception)
    {
        // Si la IA falla (timeout, error de proveedor, etc.), el ticket se
        // guarda igual con sus valores por defecto (Peticion/Pendiente/Media),
        // para no bloquear al cliente por una falla del módulo de IA.
    }

    db.Tickets.Add(ticket);
    await db.SaveChangesAsync();

    // Notificación en tiempo real si el ticket es crítico
    if (ticket.Priority == TicketPriority.Alta || ticket.Sentiment == Sentiment.Negativo)
    {
        await hub.Clients.Group(tenantId.ToString()).SendAsync("NewCriticalTicket", new
        {
            ticketId = ticket.Id,
            subject = ticket.Subject,
            priority = ticket.Priority.ToString(),
            sentiment = ticket.Sentiment?.ToString(),
            customerName = ticket.CustomerName,
            createdAtUtc = ticket.CreatedAtUtc
        });
    }

    var response = new TicketResponse(
        ticket.Id,
        ticket.CustomerName,
        ticket.CustomerEmail,
        ticket.Subject,
        ticket.Description,
        ticket.Type,
        ticket.Status,
        ticket.Priority,
        ticket.Sentiment,
        ticket.AiSummary,
        ticket.CreatedAtUtc);

    return Results.Created($"/api/v1/tickets/{ticket.Id}", response);
});

app.MapPost("/api/v1/widget/rag-search", async (RagSearchRequest request, HttpContext context, ApplicationDbContext db, IAiService ai) =>
{
    var tenantId = (Guid)context.Items["TenantId"]!;

    // 1. Generamos el embedding de la pregunta del usuario
    var questionEmbedding = await ai.GenerateEmbeddingAsync(request.Question);

    // 2. Buscamos los artículos más similares por distancia coseno, filtrado por tenant
    const double similarityThreshold = 0.5; // 1 - distancia_coseno >= 0.5 (ajustado para all-minilm)

    var candidates = await db.KnowledgeBaseArticles
        .Where(k => k.TenantId == tenantId && k.IsActive && k.Embedding != null)
        .OrderBy(k => k.Embedding!.CosineDistance(questionEmbedding))
        .Take(3)
        .Select(k => new
        {
            k.Content,
            Distance = k.Embedding!.CosineDistance(questionEmbedding)
        })
        .ToListAsync();

    var relevantArticles = candidates
        .Where(c => (1 - c.Distance) >= similarityThreshold)
        .Select(c => c.Content)
        .ToList();

    // 3. Si no hay nada suficientemente similar, no inventamos respuesta
    if (relevantArticles.Count == 0)
    {
        return Results.Ok(new RagSearchResponse(Found: false, Answer: null));
    }

    // 4. Sintetizamos la respuesta con el LLM usando solo esos artículos como contexto
    var answer = await ai.GenerateAnswerAsync(request.Question, relevantArticles);

    return Results.Ok(new RagSearchResponse(Found: true, Answer: answer));
});

// Endpoints de Autenticación
app.MapPost("/api/v1/auth/register", async (RegisterAgentRequest request, ApplicationDbContext db) =>
{
    var tenantExists = await db.Tenants.AnyAsync(t => t.Id == request.TenantId && t.IsActive);
    if (!tenantExists)
    {
        return Results.BadRequest(new { error = "El Tenant especificado no existe o está inactivo." });
    }

    var emailTaken = await db.Users.AnyAsync(u => u.TenantId == request.TenantId && u.Email == request.Email);
    if (emailTaken)
    {
        return Results.Conflict(new { error = "Ya existe un agente con ese correo en este Tenant." });
    }

    var user = new User
    {
        TenantId = request.TenantId,
        FullName = request.FullName,
        Email = request.Email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
        Role = "Agent"
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/users/{user.Id}", new { user.Id, user.FullName, user.Email, user.Role });
});

app.MapPost("/api/v1/auth/login", async (LoginRequest request, ApplicationDbContext db, IConfiguration config) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);

    if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    var jwtSection = config.GetSection("Jwt");
    var secretKey = jwtSection["SecretKey"]!;
    var expirationMinutes = int.Parse(jwtSection["ExpirationMinutes"]!);
    var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

    var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new(ClaimTypes.Email, user.Email),
        new(ClaimTypes.Role, user.Role),
        new("tenantId", user.TenantId.ToString())
    };

    var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
    var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: jwtSection["Issuer"],
        audience: jwtSection["Audience"],
        claims: claims,
        expires: expiresAt,
        signingCredentials: credentials);

    var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

    return Results.Ok(new LoginResponse(tokenString, expiresAt, user.FullName, user.Role));
});

// Endpoints protegidos para Agentes 
app.MapGet("/api/v1/tickets", async (ClaimsPrincipal user, ApplicationDbContext db, string? status, string? priority) =>
{
    var tenantIdClaim = user.FindFirst("tenantId")?.Value;
    if (tenantIdClaim is null || !Guid.TryParse(tenantIdClaim, out var tenantId))
    {
        return Results.Unauthorized();
    }

    var query = db.Tickets.Where(t => t.TenantId == tenantId);

    if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TicketStatus>(status, ignoreCase: true, out var parsedStatus))
    {
        query = query.Where(t => t.Status == parsedStatus);
    }

    if (!string.IsNullOrWhiteSpace(priority) && Enum.TryParse<TicketPriority>(priority, ignoreCase: true, out var parsedPriority))
    {
        query = query.Where(t => t.Priority == parsedPriority);
    }

    var tickets = await query
        .OrderByDescending(t => t.CreatedAtUtc)
        .Select(t => new TicketSummaryResponse(
            t.Id,
            t.CustomerName,
            t.Subject,
            t.Type.ToString(),
            t.Status.ToString(),
            t.Priority.ToString(),
            t.Sentiment != null ? t.Sentiment.ToString() : null,
            t.AiSummary,
            t.CreatedAtUtc))
        .ToListAsync();

    return Results.Ok(tickets);
})
.RequireAuthorization();

app.MapGet("/api/v1/tickets/{id:guid}", async (Guid id, ClaimsPrincipal user, ApplicationDbContext db) =>
{
    var tenantIdClaim = user.FindFirst("tenantId")?.Value;
    if (tenantIdClaim is null || !Guid.TryParse(tenantIdClaim, out var tenantId))
    {
        return Results.Unauthorized();
    }

    var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);
    if (ticket is null) return Results.NotFound();

    var response = new TicketResponse(
        ticket.Id,
        ticket.CustomerName,
        ticket.CustomerEmail,
        ticket.Subject,
        ticket.Description,
        ticket.Type,
        ticket.Status,
        ticket.Priority,
        ticket.Sentiment,
        ticket.AiSummary,
        ticket.CreatedAtUtc);

    return Results.Ok(response);
})
.RequireAuthorization();

app.MapPut("/api/v1/tickets/{id:guid}/status", async (Guid id, UpdateTicketStatusRequest request, ClaimsPrincipal user, ApplicationDbContext db) =>
{
    var tenantIdClaim = user.FindFirst("tenantId")?.Value;
    if (tenantIdClaim is null || !Guid.TryParse(tenantIdClaim, out var tenantId))
    {
        return Results.Unauthorized();
    }

    var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);
    if (ticket is null) return Results.NotFound();

    if (!string.IsNullOrWhiteSpace(request.Status))
    {
        if (!Enum.TryParse<TicketStatus>(request.Status, ignoreCase: true, out var newStatus))
        {
            return Results.BadRequest(new { error = "Status inválido. Valores permitidos: Pendiente, EnProceso, Resuelto." });
        }
        ticket.Status = newStatus;
    }

    if (request.AssignedUserId.HasValue)
    {
        ticket.AssignedUserId = request.AssignedUserId.Value;
    }

    ticket.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();

    var response = new TicketResponse(
        ticket.Id,
        ticket.CustomerName,
        ticket.CustomerEmail,
        ticket.Subject,
        ticket.Description,
        ticket.Type,
        ticket.Status,
        ticket.Priority,
        ticket.Sentiment,
        ticket.AiSummary,
        ticket.CreatedAtUtc);

    return Results.Ok(response);
})
.RequireAuthorization();

// Endpoints protegidos: KB Articles (CRUD)
var kbGroup = app.MapGroup("/api/v1/kb-articles").RequireAuthorization();

kbGroup.MapPost("/", async (UpsertKnowledgeBaseArticleRequest request, ClaimsPrincipal user, ApplicationDbContext db, IAiService ai) =>
{
    var tenantId = Guid.Parse(user.FindFirst("tenantId")!.Value);

    var article = new KnowledgeBaseArticle
    {
        TenantId = tenantId,
        Title = request.Title,
        Content = request.Content
    };

    // Generamos el embedding a partir de Título + Contenido, para que la
    // búsqueda semántica también capture coincidencias por el título.
    var textToEmbed = $"{request.Title}\n{request.Content}";
    article.Embedding = await ai.GenerateEmbeddingAsync(textToEmbed);

    db.KnowledgeBaseArticles.Add(article);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/kb-articles/{article.Id}", new KnowledgeBaseArticleResponse(
        article.Id, article.Title, article.Content, article.IsActive, article.Embedding != null, article.CreatedAtUtc));
});

kbGroup.MapGet("/", async (ClaimsPrincipal user, ApplicationDbContext db) =>
{
    var tenantId = Guid.Parse(user.FindFirst("tenantId")!.Value);

    var articles = await db.KnowledgeBaseArticles
        .Where(k => k.TenantId == tenantId)
        .OrderByDescending(k => k.CreatedAtUtc)
        .Select(k => new KnowledgeBaseArticleResponse(
            k.Id, k.Title, k.Content, k.IsActive, k.Embedding != null, k.CreatedAtUtc))
        .ToListAsync();

    return Results.Ok(articles);
});

kbGroup.MapPut("/{id:guid}", async (Guid id, UpsertKnowledgeBaseArticleRequest request, ClaimsPrincipal user, ApplicationDbContext db, IAiService ai) =>
{
    var tenantId = Guid.Parse(user.FindFirst("tenantId")!.Value);

    var article = await db.KnowledgeBaseArticles.FirstOrDefaultAsync(k => k.Id == id && k.TenantId == tenantId);
    if (article is null) return Results.NotFound();

    article.Title = request.Title;
    article.Content = request.Content;

    var textToEmbed = $"{request.Title}\n{request.Content}";
    article.Embedding = await ai.GenerateEmbeddingAsync(textToEmbed);

    article.UpdatedAtUtc = DateTime.UtcNow;

    await db.SaveChangesAsync();

    return Results.Ok(new KnowledgeBaseArticleResponse(
        article.Id, article.Title, article.Content, article.IsActive, article.Embedding != null, article.CreatedAtUtc));
});

kbGroup.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, ApplicationDbContext db) =>
{
    var tenantId = Guid.Parse(user.FindFirst("tenantId")!.Value);

    var article = await db.KnowledgeBaseArticles.FirstOrDefaultAsync(k => k.Id == id && k.TenantId == tenantId);
    if (article is null) return Results.NotFound();

    db.KnowledgeBaseArticles.Remove(article);
    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.Run();