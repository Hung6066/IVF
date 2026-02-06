using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace IVF.Gateway.Controllers;

[ApiController]
[Route("api/gateway/biometrics")]
public class BiometricAggregatorController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BiometricAggregatorController> _logger;

    public BiometricAggregatorController(
        IHttpClientFactory httpClientFactory, 
        IConfiguration configuration,
        ILogger<BiometricAggregatorController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("identify")]
    public async Task<IActionResult> Identify([FromBody] JsonElement requestBody)
    {
        // Get Cluster Destinations from Config (Manually or via YARP abstraction if accessible, manual is easier for now)
        // Hardcoded or Configured Shard URLs
        var shardUrls = new[] 
        { 
            "https://localhost:7001", // Node 1
            "https://localhost:7002"  // Node 2 (Example)
        };

        var tasks = shardUrls.Select(async url => 
        {
            try 
            {
                var client = _httpClientFactory.CreateClient();
                // Check if internal API requires Key? 
                // client.DefaultRequestHeaders.Add("X-API-KEY", "If-Needed");
                
                var response = await client.PostAsJsonAsync($"{url}/api/patients/fingerprints/identify", requestBody);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<IdentificationResult>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying shard {Url}", url);
            }
            return null;
        });

        var results = await Task.WhenAll(tasks);

        // Aggregation Logic: Find Best Match (Highest Score / Lowest FAR depending on metric)
        // Assuming Score is "Confidence" (Higher is better) or FAR (Lower is better).
        // BiometricMatcherService returns "Score" (FAR). Implementation said: "Lower is BETTER match".
        
        var validMatches = results.Where(r => r != null && r.Match).ToList();

        if (validMatches.Any())
        {
            // Find lowest Score (FAR)
            var bestMatch = validMatches.OrderBy(r => r.Score).First();
            return Ok(bestMatch);
        }

        return Ok(new IdentificationResult { Match = false });
    }
}

public class IdentificationResult 
{
    public bool Match { get; set; }
    public Guid? PatientId { get; set; }
    public int Score { get; set; }
}
