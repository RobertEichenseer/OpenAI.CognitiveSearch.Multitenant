using System.Text;
using System.Text.Json;

namespace SimpleClient;

public class SearchIndex 
{

    string _searchApiKey;
    string _searchEndpoint;
    string _searchAPIVersion;
    HttpClient _httpClient; 

    public SearchIndex(string searchAPIKey, string searchEndpoint, string searchAPIVersion)
    {
        _searchApiKey = searchAPIKey; 
        _searchEndpoint = searchEndpoint; 
        _searchAPIVersion = searchAPIVersion; 

        _httpClient = new HttpClient(); 
    }
    
    public async Task<bool> CreateIndex(string tenantId) 
    {

        //delete index if exists
        await DeleteSearchIndex(tenantId);

        string payloadTemplate = "./HttpPayload/TemplateCreateIndex.json";
        string payload = File.ReadAllText(payloadTemplate);
        payload = payload.Replace("{{indexName}}", tenantId);

        string uri = $"{_searchEndpoint}/indexes/{tenantId}/?api-version={_searchAPIVersion}";

        StringContent stringContent = new StringContent(
            payload, 
            Encoding.UTF8, 
            "application/json"
        );
        stringContent.Headers.Add("api-key", _searchApiKey);
        
        // Send the request and get the response
        HttpResponseMessage httpResponseMessage = await _httpClient.PutAsync(uri, stringContent);

        return httpResponseMessage.IsSuccessStatusCode; 

    }

    public async Task<bool> DeleteSearchIndex(string tenantId)
    {
        string uri = $"{_searchEndpoint}/indexes/{tenantId}/?api-version={_searchAPIVersion}";

        HttpRequestMessage httpRequestMessage = new HttpRequestMessage(){
            RequestUri = new Uri(uri),
            Method = HttpMethod.Delete
        };
        httpRequestMessage.Headers.Add("api-key", _searchApiKey);

        HttpResponseMessage httpResponseMessage = await _httpClient.SendAsync(httpRequestMessage);

        return httpResponseMessage.IsSuccessStatusCode;    
    }

    public async Task<bool> PopulateDocumentToSearchIndex(string tenantId, string documentId, string documentTitle, string documentUrl, float[] documentContentVector)
    {
        string payloadTemplate = "./HttpPayload/TemplatePopulateDocument.json";
        string payload = File.ReadAllText(payloadTemplate);

        payload = payload
            .Replace("{{documentId}}", documentId)
            .Replace("{{documentTitle}}", documentTitle)
            .Replace("{{documentUrl}}", documentUrl)
            .Replace("\"{{documentContentVector}}\"", $"{string.Join(",",documentContentVector)}"); 
        
        string uri = $"{_searchEndpoint}/indexes/{tenantId}/docs/index/?api-version={_searchAPIVersion}";

        StringContent stringContent = new StringContent(
            payload, 
            Encoding.UTF8, 
            "application/json"
        );
        stringContent.Headers.Add("api-key", _searchApiKey);
        
        // Send the request and get the response
        HttpResponseMessage httpResponseMessage = await _httpClient.PostAsync(uri, stringContent);

        return httpResponseMessage.IsSuccessStatusCode; 
    }

    public async Task<(
        bool success, 
        string documentId, 
        string documentTitle, 
        string documentUrl, 
        float searchScore)> GetDocumentsFromSearchIndex(string tenantId, int documentCount, float[] documentQueryVector)
    {

        string payloadTemplate = "./HttpPayload/TemplateQueryDocument.json";
        string payload = File.ReadAllText(payloadTemplate);

        payload = payload
            .Replace("\"{{documentCount}}\"", documentCount.ToString())
            .Replace("\"{{documentQueryVector}}\"", $"{string.Join(",",documentQueryVector)}"); 
        
        string uri = $"{_searchEndpoint}/indexes/{tenantId}/docs/search/?api-version={_searchAPIVersion}";

        StringContent stringContent = new StringContent(
            payload, 
            Encoding.UTF8, 
            "application/json"
        );
        stringContent.Headers.Add("api-key", _searchApiKey);
        
        // Send the request and get the response
        HttpResponseMessage httpResponseMessage = await _httpClient.PostAsync(uri, stringContent);

        if (httpResponseMessage.IsSuccessStatusCode) {
            var result = parseDocumentQueryResult(await httpResponseMessage.Content.ReadAsStringAsync());
            return(true, result.documentId, result.documentTitle, result.documentUrl, result.searchScore);
        }
        
        return (false, "", "", "", -1); 
    }

    private (
        string documentId, 
        string documentTitle, 
        string documentUrl, 
        float searchScore) parseDocumentQueryResult(string searchResult)
    {

        string documentId = "";
        string documentTitle = "";
        string documentUrl = ""; 
        float searchScore = 0; 

        JsonElement jsonElement = JsonSerializer.Deserialize<JsonElement>(searchResult);
        var returnValues = (jsonElement.GetProperty("value")).EnumerateArray().ToList();

        if (returnValues.Count > 0) {

            documentId = (returnValues.Select(x => x.GetProperty("documentId"))
                .FirstOrDefault()
                .GetString()
            ) ?? "";

            documentTitle = (returnValues.Select(x => x.GetProperty("documentTitle"))
                .FirstOrDefault()
                .GetString()
            ) ?? "";  

            documentUrl = (returnValues.Select(x => x.GetProperty("documentUrl"))
                .FirstOrDefault()
                .GetString()
            ) ?? "";

            searchScore = (returnValues.Select(x => x.GetProperty("@search.score"))
                .FirstOrDefault()
                .GetSingle()
            );
        }
        return (documentId, documentTitle, documentUrl, searchScore);
    }
}
