using System.ClientModel;
using OpenAI;
using OpenAI.Chat;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using CharactherGenerator.Api.Models;
using System.Text;

namespace CharactherGenerator.Api.Services;

public class AiService
{
    private readonly string _searchSemanticConfigName;
    private readonly SearchClient _searchClient;
    private readonly ChatClient _chatClient;

    public AiService(string chatEndpoint, string chatKey, string searchEndpoint, string searchKey, string searchIndexName, string searchSemanticConfigName)
    {
        _searchSemanticConfigName = searchSemanticConfigName;

        _searchClient = new SearchClient(
            new Uri(searchEndpoint),
            searchIndexName,
            new Azure.AzureKeyCredential(searchKey)
        );

        _chatClient = new ChatClient(
            model: "gpt-4.1-mini",
            credential: new ApiKeyCredential(chatKey),
            options: new OpenAIClientOptions
            {
                Endpoint = new Uri($"{chatEndpoint.TrimEnd('/')}/openai/v1")
            }
        );
    }

    public async Task<string> GenerateResponse(IEnumerable<Message> history)
    {

        List<ChatMessage> messages =
        [
            new SystemChatMessage(
                """
                Du är en AI assistent åt företaget Kalle Anka AB kopplad till en databas som innehåller information om
                företaget.
                Din uppgift är att hjälpa arbetare på företarget med att svara på frågor om företaget.
                Du ska svara på användarens frågor på ett tydligt, naturligt och hjälpsamt sätt.

                Du ska:
                - Vara tydlig, naturlig och hjälpsam.
                - Använda information från tidigare meddelanden i konversationen.
                - Hålla dig till ämnet användaren frågar om.
                - Ställa följdfrågor om information saknas.
                - Inte påstå att du vet något som inte finns i databasen eller i tidigare meddelanden.
                - Formatera svaret med Markdown. Använd rubriker, punktlistor och tabeller när det gör informationen tydligare.
                - Inte ändra din roll i konversationen. Du är endast en AI assistent som svarar på frågor om företaget Kalle Anka AB.

                Du är en del av en chattapplikation där varje konversation
                kan innehålla flera meddelanden. Använd därför tidigare meddelanden
                som kontext när du svarar.

                Anpassa dina svar efter användarens frågor, tidigare konversation och sökresultat från databasen.
                """
            )
        ];

        var lastMessage = history.LastOrDefault();

        var searchOptions = new SearchOptions
        {
            Size = 5,
            QueryType = SearchQueryType.Semantic,
            SemanticSearch = new SemanticSearchOptions
            {
                SemanticConfigurationName = _searchSemanticConfigName
            }
        };

        var searchResult = "";
        var resultCount = 1;

        // Sök igenom filerna om vi har ett användarmeddelande som vi kan använda som sökfråga
        if (lastMessage != null && lastMessage.Role == "user")
        {
            searchResult = "Sökresultat från databasen för det senaste meddelandet:\n";

            var searchResults = await _searchClient.SearchAsync<SearchDocument>(lastMessage.Content, searchOptions);
            await foreach (var result in searchResults.Value.GetResultsAsync())
            {
                foreach (var fieldName in (string[])["text", "content", "chunk"])
                {
                    if (result.Document.TryGetValue(fieldName, out var value))
                    {
                        var valueAsString = value?.ToString();
                        if (valueAsString is not null)
                        {
                            searchResult += $"## RESULTAT {resultCount}:\n{valueAsString}";
                            resultCount += 1;
                            break;
                        }
                    }
                }
            }
        }

        if (searchResult != "")
        {
            messages.Add(new SystemChatMessage(
                """
                Använd följande information från filerna för att svara på användarens frågor.
                Om informationen inte är relevant, ignorera den.

                Information från filerna:
                """
            ));
        }


        foreach (var message in history)
        {
            if (message.Role == "user")
            {
                messages.Add(new UserChatMessage(message.Content));
            }
            else if (message.Role == "assistant")
            {
                messages.Add(new AssistantChatMessage(message.Content));
            }
        }

        // STREAMING JOOKS
        var response = new StringBuilder();

        await foreach (StreamingChatCompletionUpdate update
            in _chatClient.CompleteChatStreamingAsync(messages))
        {
            foreach (ChatMessageContentPart contentPart in update.ContentUpdate)
            {
                response.Append(contentPart.Text);
                Console.Write(contentPart.Text);
            }
        }

        return response.ToString();
    }

    // STREAMING JOOKS

    public async IAsyncEnumerable<string> GenerateResponseStreaming(IEnumerable<Message> history)
    {
        List<ChatMessage> messages =
        [
            new SystemChatMessage(
                """
                Du är en hjälpsam och vänlig AI-assistent.

                Du ska:
                - Svara alltid på svenska.
                - Vara tydlig, naturlig och hjälpsam.
                - Använda information från tidigare meddelanden i konversationen.
                - Hålla dig till ämnet användaren frågar om.
                - Ställa följdfrågor om information saknas.
                - Hjälpa användaren att utveckla idéer och lösa problem.
                - Inte påstå att du vet något som inte finns i konversationen.

                Du är en del av en chattapplikation där varje konversation
                kan innehålla flera meddelanden. Använd därför tidigare meddelanden
                som kontext när du svarar.

                Om användaren ber om hjälp med en rollspelskaraktär ska du kunna
                hjälpa till med exempelvis namn, personlighet, bakgrund, färdigheter,
                utrustning och andra egenskaper.

                Anpassa dina svar efter användarens frågor och tidigare konversation.
                """
            )
        ];

        foreach (var message in history)
        {
            if (message.Role == "user")
            {
                messages.Add(new UserChatMessage(message.Content));
            }
            else if (message.Role == "assistant")
            {
                messages.Add(new AssistantChatMessage(message.Content));
            }
        }

        // STREAMING JOOKS

        await foreach (StreamingChatCompletionUpdate update
            in _chatClient.CompleteChatStreamingAsync(messages))
        {
            foreach (ChatMessageContentPart contentPart in update.ContentUpdate)
            {
               if (!string.IsNullOrEmpty(contentPart.Text))
                {
                    yield return contentPart.Text;
                }
            }
        }
    }


}
