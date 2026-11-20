using Azure;
using Azure.Search.Documents.Indexes;

namespace CharactherGenerator.Api.Services;

public class SearchIndexerService
{
	private readonly SearchIndexerClient _indexerClient;
	private readonly string _indexerName;

	public SearchIndexerService(
		string endpoint,
		string apiKey,
		string indexerName)
	{
		_indexerClient = new SearchIndexerClient(
			new Uri(endpoint),
			new AzureKeyCredential(apiKey)
		);

		_indexerName = indexerName;
	}

	public async Task RunIndexer()
	{
		await _indexerClient.RunIndexerAsync(_indexerName);
	}
}