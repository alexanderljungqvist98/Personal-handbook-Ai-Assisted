using CharactherGenerator.Api.Services;
using CharactherGenerator.Api.Data;
using CharactherGenerator.Api.Models;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Azure.Storage.Blobs;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(Options =>
Options.UseSqlite("Data Source=characther-generator.db"));

builder.Services.AddCors(options =>
{
    options.AddPolicy("React", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var endpoint = builder.Configuration["AzureAI:Endpoint"]
    ?? throw new InvalidOperationException("AzureAI:Endpoint saknas.");

var apiKey = builder.Configuration["AzureAI:ApiKey"]
    ?? throw new InvalidOperationException("AzureAI:ApiKey saknas.");

var endpointsearch = builder.Configuration["AzureAI:EndpointSearch"]
    ?? throw new InvalidOperationException("AzureAI:EndpointSearch saknas.");

var apiKeySearch = builder.Configuration["AzureAI:ApiKeySearch"]
    ?? throw new InvalidOperationException("AzureAI:ApiKeySearch saknas.");

var searchIndex = builder.Configuration["AzureAI:Index"]
    ?? throw new InvalidOperationException("AzureAI:Index saknas.");

var searchSemantic = builder.Configuration["AzureAI:Sematic"]
    ?? throw new InvalidOperationException("AzureAI:Semantic saknas.");

var blobConnectionString = builder.Configuration["AzureStorage:ConnectionString"]
    ?? throw new InvalidOperationException("AzureStorage:ConnectionString saknas.");

var blobContainerName = builder.Configuration["AzureStorage:ContainerName"]
    ?? throw new InvalidOperationException("AzureStorage:ContainerName saknas.");

var indexerName = builder.Configuration["AzureAI:Indexer"]
    ?? throw new InvalidOperationException("AzureAI:Indexer saknas.");


builder.Services.AddSingleton<AiService>(sp =>
    new AiService(endpoint, apiKey, endpointsearch, apiKeySearch, searchIndex, searchSemantic));

builder.Services.AddSingleton<SearchIndexerService>(sp =>
    new SearchIndexerService(
        endpointsearch,
        apiKeySearch,
        indexerName
    ));

var app = builder.Build();

app.UseCors("React");

app.MapPost("/api/chats", async (AppDbContext db) =>
{
    var chat = new Chat
    {
        Title = "Ny chatt",
        CreatedAt = DateTime.UtcNow
    };

    db.Chats.Add(chat);
    await db.SaveChangesAsync();

    return Results.Ok(chat);
});

app.MapGet("/api/chats", async (AppDbContext db) =>
{
    var chats = await db.Chats
    .OrderByDescending(chat => chat.CreatedAt)
    .ToListAsync();

    return Results.Ok(chats);

});

app.MapPost("/api/chats/{chatId}/messages", async (
    int chatId,
    SendMessageRequest request,
    AppDbContext db,
    AiService aiService,
    HttpContext context) =>
{
    var chat = await db.Chats.FindAsync(chatId);

if (chat is null)
{
    context.Response.StatusCode = 404;
    return;
}

    var hasPreviousUserMessage = await db.Messages
        .AnyAsync(message =>
            message.ChatId == chatId &&
            message.Role == "user");

    var userMessage = new Message
    {
        ChatId = chatId,
        Role = "user",
        Content = request.Content,
        CreatedAt = DateTime.UtcNow
    };

    // Skapa automatiskt chattnamn från första meddelandet
    if (!hasPreviousUserMessage)
    {
        var title = request.Content.Trim();

        if (title.Length > 35)
        {
            title = title[..35] + "...";
        }

        chat.Title = title;
    }

    db.Messages.Add(userMessage);

    await db.SaveChangesAsync();

    var history = await db.Messages
        .Where(message => message.ChatId == chatId)
        .OrderBy(message => message.CreatedAt)
        .ToListAsync();

   context.Response.ContentType = "text/plain; charset=utf-8";

   // STREAMING JOOKS

var responseBuilder = new StringBuilder();

await foreach (var chunk in aiService.GenerateResponseStreaming(history))
{
    responseBuilder.Append(chunk);

    await context.Response.WriteAsync(chunk);
    await context.Response.Body.FlushAsync();
}

var aiResponse = responseBuilder.ToString();

var assistantMessage = new Message
{
    ChatId = chatId,
    Role = "assistant",
    Content = aiResponse,
    CreatedAt = DateTime.UtcNow
};

db.Messages.Add(assistantMessage);
await db.SaveChangesAsync();
});

app.MapGet("/api/chats/{chatId}/messages", async (
    int chatId,
    AppDbContext db) =>
{
    var messages = await db.Messages
        .Where(message => message.ChatId == chatId)
        .OrderBy(message => message.CreatedAt)
        .Select(message => new
        {
            message.Id,
            message.ChatId,
            message.Role,
            message.Content,
            message.CreatedAt
        })
        .ToListAsync();

    return Results.Ok(messages);
});

app.MapDelete("/api/chats/{chatId}", async (
    int chatId,
    AppDbContext db) =>
{
    var chat = await db.Chats.FindAsync(chatId);

    if (chat is null)
    {
        return Results.NotFound();
    }

    db.Chats.Remove(chat);
    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.MapPost("/api/documents", async (IFormFileCollection files, SearchIndexerService searchIndexerService) =>
{
    if (files.Count == 0)
    {
        return Results.BadRequest("Välj minst en fil.");
    }

    var blobServiceClient = new BlobServiceClient(blobConnectionString);

    var containerClient =
        blobServiceClient.GetBlobContainerClient(blobContainerName);

    foreach (var file in files)
    {
        var blobClient = containerClient.GetBlobClient(file.FileName);
        using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(stream, overwrite: true);
    }

    await searchIndexerService.RunIndexer();

    return Results.Ok(new
    {
        fileNames = files.Select(file => file.FileName),
        message = "Dokumenten har laddats upp."
    });
})
.DisableAntiforgery();

app.MapGet("/api/documents", async () =>
{
    var blobServiceClient = new BlobServiceClient(blobConnectionString);
    var containerClient =
        blobServiceClient.GetBlobContainerClient(blobContainerName);
    var blobs = containerClient.GetBlobs();
    var documentList = blobs.Select(blob => new
    {
        fileName = blob.Name,
        url = $"{containerClient.Uri}/{blob.Name}"
    });
    return Results.Ok(documentList);
});

app.MapDelete("/api/documents/{fileName}", async (string fileName, SearchIndexerService searchIndexerService) =>
{
    var blobServiceClient = new BlobServiceClient(blobConnectionString);
    var containerClient =
        blobServiceClient.GetBlobContainerClient(blobContainerName);
    var blobClient = containerClient.GetBlobClient(fileName);
    await blobClient.DeleteIfExistsAsync();

    await searchIndexerService.RunIndexer();

    return Results.Ok(new
    {
        fileName = fileName,
        message = "Dokumentet har tagits bort."
    });
});

app.Run();
