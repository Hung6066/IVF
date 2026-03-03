using System.Net.Http.Json;
using System.Text.Json;
using IVF.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Bot detection using Google reCAPTCHA v3 (invisible, score-based)
/// and server-side request analysis for known bot patterns.
/// </summary>
public sealed class BotDetectionService(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<BotDetectionService> logger) : IBotDetectionService
{
    private const string RecaptchaVerifyUrl = "https://www.google.com/recaptcha/api/siteverify";

    public async Task<BotDetectionResult> ValidateCaptchaAsync(string token, string? ipAddress, CancellationToken ct = default)
    {
        var secretKey = config["BotDetection:SecretKey"];
        if (string.IsNullOrEmpty(secretKey))
        {
            logger.LogDebug("reCAPTCHA not configured — skipping bot validation");
            return new BotDetectionResult(false, 1.0m, null);
        }

        try
        {
            using var client = httpClientFactory.CreateClient();
            var response = await client.PostAsync(RecaptchaVerifyUrl, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = secretKey,
                ["response"] = token,
                ["remoteip"] = ipAddress ?? ""
            }), ct);

            var result = await response.Content.ReadFromJsonAsync<RecaptchaResponse>(ct);
            if (result is null)
                return new BotDetectionResult(false, 0.5m, "Failed to parse reCAPTCHA response");

            if (!result.Success)
            {
                logger.LogWarning("reCAPTCHA validation failed: {Errors}", string.Join(", ", result.ErrorCodes ?? []));
                return new BotDetectionResult(true, 0.0m, "reCAPTCHA validation failed");
            }

            var threshold = config.GetValue("BotDetection:ScoreThreshold", 0.5m);
            var isBot = result.Score < (double)threshold;

            if (isBot)
                logger.LogWarning("Bot detected: reCAPTCHA score {Score} below threshold {Threshold}", result.Score, threshold);

            return new BotDetectionResult(isBot, (decimal)result.Score, isBot ? $"Low score: {result.Score:F2}" : null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "reCAPTCHA validation error");
            return new BotDetectionResult(false, 0.5m, "Validation error — allowing request");
        }
    }

    public BotDetectionResult AnalyzeRequest(RequestSecurityContext context)
    {
        var botIndicators = 0;
        var reasons = new List<string>();

        // 1. Missing or empty User-Agent
        if (string.IsNullOrEmpty(context.UserAgent))
        {
            botIndicators += 2;
            reasons.Add("no_user_agent");
        }

        // 2. Known bot user agents
        if (context.UserAgent is not null)
        {
            var ua = context.UserAgent.ToLowerInvariant();
            if (ua.Contains("bot") || ua.Contains("crawler") || ua.Contains("spider") ||
                ua.Contains("headless") || ua.Contains("phantom") || ua.Contains("selenium") ||
                ua.Contains("puppeteer") || ua.Contains("playwright"))
            {
                botIndicators += 3;
                reasons.Add("bot_user_agent");
            }

            // Very short user agent (likely crafted)
            if (ua.Length < 20)
            {
                botIndicators++;
                reasons.Add("short_user_agent");
            }
        }

        // 3. Missing standard headers
        if (context.AdditionalSignals is not null)
        {
            if (!context.AdditionalSignals.ContainsKey("accept_language"))
            {
                botIndicators++;
                reasons.Add("no_accept_language");
            }
        }

        var isBot = botIndicators >= 3;
        var score = Math.Max(0, 1.0m - (botIndicators * 0.2m));

        return new BotDetectionResult(isBot, score, isBot ? string.Join(", ", reasons) : null);
    }

    private record RecaptchaResponse(
        bool Success,
        double Score,
        string? Action,
        string? Hostname,
        DateTime? ChallengeTs,
        string[]? ErrorCodes);
}
