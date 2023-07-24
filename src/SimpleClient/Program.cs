using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using SimpleClient;
using Azure;
using Azure.AI.OpenAI;

IHost consoleHost = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services => {
        services.AddTransient<Main>();
    })
    .Build();

Main main = consoleHost.Services.GetRequiredService<Main>();
await main.ExecuteAsync(args);

class Main
{
    ILogger<Main> _logger;

    string _embeddingEndpoint = Environment.GetEnvironmentVariable("CS_EMBEDDING_ENDPOINT") ?? "http://Null";
    string _embeddingApiKey = Environment.GetEnvironmentVariable("CS_EMBEDDING_APIKEY") ?? "API-KEY-NOT-SET";
    string _embeddingDeploymentName = Environment.GetEnvironmentVariable("CS_EMBEDDING_DEPLOYMENTNAME") ?? "API-MODELDEPLOYMENTNAME NOT SET";

    string _searchApiKey = Environment.GetEnvironmentVariable("CS_SEARCH_APIKEY") ?? "SEARCH-APIKEY-NOT-SET";
    string _searchEndpoint = Environment.GetEnvironmentVariable("CS_SEARCH_ENDPOINT") ?? "http://Null";
    string _searchAPIVersion= Environment.GetEnvironmentVariable("CS_SEARCH_APIVERSION") ?? "SEARCH-APIVERSION-NOT-SET";

    SearchIndex _searchIndex;

    OpenAIClient? _openAIClient = null; 

    public Main (ILogger<Main> logger)
    {
        _logger = logger;
        _searchIndex = new SearchIndex(_searchApiKey, _searchEndpoint, _searchAPIVersion); 
    }

    public async Task ExecuteAsync(string[] args)
    {
        //**********************************************
        //* TENANT: EMPIRE
        //**********************************************
        //Create and store information
        string tenantId = "empire";
        await _searchIndex.CreateIndex(tenantId);

        string documentId = Guid.NewGuid().ToString();
        string documentTitle = "[Empire]-[TopSecret]-[Archive Planet]";
        string documentUrl = $"https://www.empire.com/query?documentId={documentId}";
        string documentContent = "The confidential coordinates from the hidden planet Scarif where the Death Star blue prints, including it's vulnerabilities, are archived.";
        float[] documentEmbedding = await CreateEmbedding(documentContent); 
        
        await _searchIndex.PopulateDocumentToSearchIndex(tenantId, documentId, documentTitle, documentUrl, documentEmbedding);

        //**********************************************
        //* TENANT: REPUBLIC
        //**********************************************
        //Create and store information
        tenantId = "republic";
        await _searchIndex.CreateIndex(tenantId);

        documentId = Guid.NewGuid().ToString();
        documentTitle = "[Republic]-[TopSecret]-[Planet of Luke's Jedi temple]";
        documentUrl = $"https://www.republic.com/query?documentId={documentId}";
        documentContent = "Luke Skywalker built his Jedi temple on the planet Ossus, a world rich in the Force and with a long history of Jedi presence";
        documentEmbedding = await CreateEmbedding(documentContent);
        
        await _searchIndex.PopulateDocumentToSearchIndex(tenantId, documentId, documentTitle, documentUrl, documentEmbedding);

        //**********************************************
        //* TENANT: AGNOSTIC
        //**********************************************
        //Create and store information in both tenants
        documentId = Guid.NewGuid().ToString();
        documentTitle = "[Common]-[No Security Clearance]-[Coruscant]";
        documentUrl = $"https://www.open.com/query?documentId={documentId}";
        documentContent = "Coruscant is a planet-wide ecumenopolis that serves as the capital and seat of government for the Republic and Empire, as well as the headquarters of the Jedi Order.";
        documentEmbedding = await CreateEmbedding(documentContent);
        
        tenantId = "empire";
        await _searchIndex.PopulateDocumentToSearchIndex(tenantId, documentId, documentTitle, documentUrl, documentEmbedding);

        tenantId = "republic";
        await _searchIndex.PopulateDocumentToSearchIndex(tenantId, documentId, documentTitle, documentUrl, documentEmbedding);


        //**********************************************
        //* QUERY
        //**********************************************
        //Create query vector
        string tenantQuery = "Location of secret planets?";
        float[] tenantQueryVector = await CreateEmbedding(tenantQuery);
        string agnosticQuery = "On which planet all politicians come together?";
        float[] agnosticQueryVector = await CreateEmbedding(agnosticQuery);
        
        //Tenant specific query: Tenant "Empire"
        Console.WriteLine($"Query: {tenantQuery}");
        tenantId = "empire";
        var searchResult = await _searchIndex.GetDocumentsFromSearchIndex(tenantId, 1, tenantQueryVector);
        Console.WriteLine($"Tenant: {tenantId} - {searchResult.documentTitle}");
        
        //Tenant specific query: Tenant "Republic"
        tenantId = "republic";
        searchResult = await _searchIndex.GetDocumentsFromSearchIndex(tenantId, 1, tenantQueryVector);
        Console.WriteLine($"Tenant: {tenantId} - {searchResult.documentTitle}");
        
        //Agnostic query        
        Console.WriteLine($"\nQuery: {agnosticQuery}");
        tenantId = "empire";
        searchResult = await _searchIndex.GetDocumentsFromSearchIndex(tenantId, 1, agnosticQueryVector);
        Console.WriteLine($"Tenant: {tenantId} - {searchResult.documentTitle}");
        
        tenantId = "republic";
        searchResult = await _searchIndex.GetDocumentsFromSearchIndex(tenantId, 1, agnosticQueryVector);
        Console.WriteLine($"Tenant: {tenantId} - {searchResult.documentTitle}");
        
    }

    private async Task<float[]> CreateEmbedding(string documentContent)
    {
        if (_openAIClient == null)
        {
            AzureKeyCredential azureKeyCredential = new AzureKeyCredential(_embeddingApiKey);
            _openAIClient = new OpenAIClient(
                new Uri(_embeddingEndpoint),
                azureKeyCredential
            );
        }

        //Calculate Embedding
        EmbeddingsOptions embeddingsOptions; 
        embeddingsOptions = new EmbeddingsOptions(documentContent);
        Response<Embeddings> embedding = await _openAIClient.GetEmbeddingsAsync(_embeddingDeploymentName, embeddingsOptions); 
        return embedding.Value.Data[0].Embedding.ToArray<float>();
        
    }
}
