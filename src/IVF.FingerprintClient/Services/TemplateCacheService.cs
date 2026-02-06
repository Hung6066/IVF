using System.Net.Http.Json;
using System.Text.Json;

namespace IVF.FingerprintClient.Services;

public class TemplateCacheService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private List<PatientFingerprintDto> _cache = new();
    
    public bool IsLoaded { get; private set; }
    public int TemplateCount => _cache.Count;

    public TemplateCacheService(string baseUrl, string apiKey)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _apiKey = apiKey;
        _httpClient = new HttpClient();
        
        // Setup API Key authentication if used by backend
        // (Note: Backend currently checks Query string or Header for SignalR, need to confirm for REST API)
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
    }

    public async Task RefreshCacheAsync()
    {
        try
        {
            var url = $"{_baseUrl}/api/patients/fingerprints/all?apiKey={_apiKey}";
            var templates = await _httpClient.GetFromJsonAsync<List<PatientFingerprintDto>>(url);
            
            if (templates != null)
            {
                _cache = templates;
                IsLoaded = true;
            }
        }
        catch (Exception ex)
        {
            // Log error
            System.Diagnostics.Debug.WriteLine($"Error fetching templates: {ex.Message}");
            throw;
        }
    }

    public List<PatientFingerprintDto> GetAllTemplates()
    {
        return _cache;
    }

    public (PatientFingerprintDto? Match, int Score) FindBestMatch(DPFP.FeatureSet features, DPFP.Verification.Verification verificator)
    {
        var result = new DPFP.Verification.Verification.Result();
        var matches = new List<(PatientFingerprintDto Template, int Score)>();

        foreach (var t in _cache)
        {
            try 
            {
                var bytes = Convert.FromBase64String(t.TemplateData);
                var template = new DPFP.Template();
                template.DeSerialize(bytes);

                verificator.Verify(features, template, ref result);
                
                if (result.Verified)
                {
                    matches.Add((t, result.FARAchieved));
                }
            }
            catch { continue; }
        }

        if (matches.Any())
        {
            var best = matches.OrderBy(m => m.Score).First();
            return (best.Template, best.Score);
        }

        return (null, 0);
    }
}

public class PatientFingerprintDto
{
    public Guid PatientId { get; set; }
    public string FingerType { get; set; } = string.Empty;
    public string TemplateData { get; set; } = string.Empty;
}
