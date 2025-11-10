using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CarRental.Api.Services;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/recommendations")]
public class RecommendationsController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RecommendationsController> _logger;
    private readonly ISettingsService _settingsService;

    private const string AnthropicApiKeySetting = "anthropic.apiKey";

    public RecommendationsController(
        IHttpClientFactory httpClientFactory,
        ISettingsService settingsService,
        ILogger<RecommendationsController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settingsService = settingsService;
        _logger = logger;
    }

    [HttpPost("ai")]
    public async Task<IActionResult> GetAIRecommendations([FromBody] AIRecommendationRequest request)
    {
        if (request is null)
        {
            return BadRequest("Request payload is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Requirements))
        {
            return BadRequest("Requirements are required.");
        }

        var vehicles = (request.AvailableVehicles ?? new List<VehicleData>())
            .Select(NormalizeVehicle)
            .Where(v => v is not null)
            .Select(v => v!)
            .ToList();

        if (vehicles.Count == 0)
        {
            return BadRequest("No vehicles supplied for recommendations.");
        }

        var mode = (request.Mode ?? "claude").Trim().ToLowerInvariant();

        if (string.Equals(mode, "free", StringComparison.Ordinal))
        {
            var freeResult = BuildRuleBasedRecommendations(request.Requirements, vehicles, request.Language);
            return Ok(freeResult);
        }

        try
        {
            var apiKey = await _settingsService.GetValueAsync(AnthropicApiKeySetting);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Anthropic API key missing. Falling back to rule-based logic.");
                var fallbackResult = BuildRuleBasedRecommendations(request.Requirements, vehicles, request.Language);
                return Ok(fallbackResult);
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            var languageCode = string.IsNullOrWhiteSpace(request.Language)
                ? "en"
                : request.Language.Trim().ToLowerInvariant();

            var prompts = GetLanguagePrompts(languageCode);
            var prompt = BuildPrompt(request, prompts, languageCode, mode, vehicles);

            var payload = new AnthropicMessageRequest
            {
                Model = "claude-sonnet-4-20250514",
                MaxTokens = 2048,
                Temperature = 0.7m,
                Messages = new[]
                {
                    new AnthropicMessage
                    {
                        Role = "user",
                        Content = prompt
                    }
                }
            };

            using var response = await httpClient.PostAsJsonAsync(
                "https://api.anthropic.com/v1/messages",
                payload);

            if (!response.IsSuccessStatusCode)
            {
                var failureBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Anthropic API call failed with status {StatusCode}. Body: {Body}",
                    response.StatusCode,
                    failureBody);
                var fallbackResult = BuildRuleBasedRecommendations(request.Requirements, vehicles, languageCode);
                fallbackResult.Summary = string.Concat(
                    fallbackResult.Summary,
                    " ",
                    GetLanguagePrompts(languageCode).FallbackNotice);
                return Ok(fallbackResult);
            }

            var result = await response.Content.ReadFromJsonAsync<ClaudeResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result is null)
            {
                _logger.LogError("Anthropic API returned an empty response.");
                var fallbackResult = BuildRuleBasedRecommendations(request.Requirements, vehicles, languageCode);
                fallbackResult.Summary = string.Concat(
                    fallbackResult.Summary,
                    " ",
                    GetLanguagePrompts(languageCode).FallbackNotice);
                return Ok(fallbackResult);
            }

            var textContent = result.Content
                ?.FirstOrDefault(c => string.Equals(c.Type, "text", StringComparison.OrdinalIgnoreCase))
                ?.Text;

            if (string.IsNullOrWhiteSpace(textContent))
            {
                _logger.LogError("Anthropic API returned no text content.");
                var fallbackResult = BuildRuleBasedRecommendations(request.Requirements, vehicles, languageCode);
                fallbackResult.Summary = string.Concat(
                    fallbackResult.Summary,
                    " ",
                    GetLanguagePrompts(languageCode).FallbackNotice);
                return Ok(fallbackResult);
            }

            var jsonText = textContent
                .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("```", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();

            AIRecommendationResult? recommendations;
            try
            {
                recommendations = JsonSerializer.Deserialize<AIRecommendationResult>(
                    jsonText,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Failed to parse AI response JSON: {Raw}", jsonText);
                recommendations = BuildRuleBasedRecommendations(request.Requirements, vehicles, languageCode);
                recommendations.Summary = string.Concat(
                    recommendations.Summary,
                    " ",
                    GetLanguagePrompts(languageCode).FallbackNotice);
                return Ok(recommendations);
            }

            if (recommendations is null)
            {
                _logger.LogError("AI recommendation result was null after parsing.");
                return StatusCode(500, "AI returned no recommendations.");
            }

            if (result.Usage is not null)
            {
                var estimatedCost =
                    (result.Usage.InputTokens * 0.003m / 1000m) +
                    (result.Usage.OutputTokens * 0.015m / 1000m);

                _logger.LogInformation(
                    "AI Recommendations completed. Mode: {Mode}, Language: {Language}, InputTokens: {InputTokens}, OutputTokens: {OutputTokens}, EstimatedCost: {Cost:F4}",
                    mode,
                    languageCode,
                    result.Usage.InputTokens,
                    result.Usage.OutputTokens,
                    estimatedCost);
            }

            recommendations.Mode = mode;
            return Ok(recommendations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting AI recommendations");
            var fallback = BuildRuleBasedRecommendations(
                request.Requirements,
                (request.AvailableVehicles ?? new List<VehicleData>())
                    .Select(NormalizeVehicle)
                    .Where(v => v is not null)
                    .Select(v => v!)
                    .ToList(),
                request.Language);
            fallback.Summary = string.Concat(
                fallback.Summary,
                " ",
                GetLanguagePrompts(request.Language ?? "en").FallbackNotice);
            return Ok(fallback);
        }
    }

    private static string BuildPrompt(
        AIRecommendationRequest request,
        LanguagePrompts prompts,
        string languageCode)
    {
        var vehiclesJson = JsonSerializer.Serialize(
            request.AvailableVehicles ?? new List<VehicleData>(),
            new JsonSerializerOptions { WriteIndented = true });

        var sb = new StringBuilder();
        sb.AppendLine(prompts.SystemRole);
        sb.AppendLine(prompts.CustomerRequirements);
        sb.AppendLine(request.Requirements);
        sb.AppendLine(prompts.AvailableVehicles);
        sb.AppendLine(vehiclesJson);
        sb.AppendLine(prompts.Instructions);
        sb.AppendLine($"IMPORTANT: Respond in {GetLanguageName(languageCode)} language.");
        sb.AppendLine("Format your response as JSON:");
        sb.AppendLine(@"{");
        sb.AppendLine(@"  ""recommendations"": [");
        sb.AppendLine(@"    {");
        sb.AppendLine(@"      ""vehicleId"": ""123"",");
        sb.AppendLine(@"      ""make"": ""Toyota"",");
        sb.AppendLine(@"      ""model"": ""Camry"",");
        sb.AppendLine(@"      ""rank"": 1,");
        sb.AppendLine(@"      ""matchScore"": 95,");
        sb.AppendLine(@"      ""reasoning"": ""Detailed explanation..."",");
        sb.AppendLine(@"      ""pros"": [""reason 1"", ""reason 2""],");
        sb.AppendLine(@"      ""cons"": [""consideration 1""],");
        sb.AppendLine(@"      ""totalCost"": 450.00");
        sb.AppendLine(@"    }");
        sb.AppendLine(@"  ],");
        sb.AppendLine(@"  ""summary"": ""Overall recommendation summary""");
        sb.AppendLine(@"}");
        sb.AppendLine("Respond ONLY with valid JSON, no additional text.");

        return sb.ToString();
    }

    private static string BuildPrompt(
        AIRecommendationRequest request,
        LanguagePrompts prompts,
        string languageCode,
        string mode,
        List<NormalizedVehicle> vehicles)
    {
        var vehiclesJson = JsonSerializer.Serialize(
            vehicles,
            new JsonSerializerOptions { WriteIndented = true });

        var sb = new StringBuilder();
        sb.AppendLine(prompts.SystemRole);
        sb.AppendLine(prompts.CustomerRequirements);
        sb.AppendLine(request.Requirements);
        sb.AppendLine(prompts.AvailableVehicles);
        sb.AppendLine(vehiclesJson);
        sb.AppendLine(prompts.Instructions);
        sb.AppendLine($"Mode selected: {mode}");
        sb.AppendLine($"IMPORTANT: Respond in {GetLanguageName(languageCode)} language.");
        sb.AppendLine("Format your response as JSON:");
        sb.AppendLine(@"{");
        sb.AppendLine(@"  ""recommendations"": [");
        sb.AppendLine(@"    {");
        sb.AppendLine(@"      ""vehicleId"": ""123"",");
        sb.AppendLine(@"      ""make"": ""Toyota"",");
        sb.AppendLine(@"      ""model"": ""Camry"",");
        sb.AppendLine(@"      ""rank"": 1,");
        sb.AppendLine(@"      ""matchScore"": 95,");
        sb.AppendLine(@"      ""reasoning"": ""Detailed explanation..."",");
        sb.AppendLine(@"      ""pros"": [""reason 1"", ""reason 2""],");
        sb.AppendLine(@"      ""cons"": [""consideration 1""],");
        sb.AppendLine(@"      ""totalCost"": 450.00");
        sb.AppendLine(@"    }");
        sb.AppendLine(@"  ],");
        sb.AppendLine(@"  ""summary"": ""Overall recommendation summary""");
        sb.AppendLine(@"}");
        sb.AppendLine("Respond ONLY with valid JSON, no additional text.");

        return sb.ToString();
    }

    private static LanguagePrompts GetLanguagePrompts(string language)
    {
        var map = new Dictionary<string, LanguagePrompts>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new(
                "You are an AI assistant helping customers choose the most suitable rental cars.",
                "Customer requirements (in customer language):",
                "Available vehicles (JSON):",
                "Consider budget, capacity, trip type, vehicle features, mode (free/claude/premium), and language. Provide match percentage, ranking, reasoning, pros, cons, and estimated total cost for each recommendation.",
                "I could not reach the AI service, so here is a curated recommendation instead.",
                "Here are a few great matches I selected for you:"),
            ["es"] = new(
                "Eres un asistente de IA que ayuda a los clientes a elegir el auto de alquiler más adecuado.",
                "Requisitos del cliente (en el idioma del cliente):",
                "Vehículos disponibles (JSON):",
                "Considera presupuesto, capacidad, tipo de viaje, características del vehículo, modo (free/claude/premium) e idioma. Proporciona porcentaje de coincidencia, ranking, razonamiento, pros, contras y costo total estimado para cada recomendación.",
                "No pude contactar al servicio de IA, así que aquí tienes una recomendación seleccionada.",
                "Aquí tienes algunas opciones que seleccioné para ti:"),
            ["pt"] = new(
                "Você é um assistente de IA que ajuda os clientes a escolher o carro de aluguel mais adequado.",
                "Requisitos do cliente (no idioma do cliente):",
                "Veículos disponíveis (JSON):",
                "Considere orçamento, capacidade, tipo de viagem, características do veículo, modo (free/claude/premium) e idioma. Forneça porcentagem de compatibilidade, ranking, raciocínio, prós, contras e custo total estimado para cada recomendação.",
                "Não foi possível acessar o serviço de IA, então aqui está uma recomendação selecionada.",
                "Aqui estão algumas combinações que selecionei para você:"),
            ["fr"] = new(
                "Vous êtes un assistant IA qui aide les clients à choisir la voiture de location la plus adaptée.",
                "Exigences du client (dans la langue du client) :",
                "Véhicules disponibles (JSON) :",
                "Tenez compte du budget, de la capacité, du type de voyage, des caractéristiques du véhicule, du mode (free/claude/premium) et de la langue. Fournissez le pourcentage de correspondance, le classement, le raisonnement, les avantages, les inconvénients et le coût total estimé pour chaque recommandation.",
                "Je n'ai pas pu joindre le service d'IA, voici donc une recommandation sélectionnée.",
                "Voici quelques options que j'ai sélectionnées pour vous :"),
            ["de"] = new(
                "Sie sind ein KI-Assistent, der Kunden hilft, das passende Mietfahrzeug auszuwählen.",
                "Kundenanforderungen (in der Sprache des Kunden):",
                "Verfügbare Fahrzeuge (JSON):",
                "Berücksichtigen Sie Budget, Kapazität, Reiseart, Fahrzeugmerkmale, Modus (free/claude/premium) und Sprache. Geben Sie Übereinstimmungsprozentsatz, Rang, Begründung, Vorteile, Nachteile und geschätzte Gesamtkosten für jede Empfehlung an.",
                "Ich konnte den KI-Dienst nicht erreichen, daher erhalten Sie eine kuratierte Empfehlung.",
                "Hier sind einige passende Optionen, die ich für Sie ausgewählt habe:"),
        };

        return map.TryGetValue(language, out var prompts)
            ? prompts
            : map["en"];
    }

    private static string GetLanguageName(string code) => code switch
    {
        "es" => "Spanish",
        "pt" => "Portuguese",
        "fr" => "French",
        "de" => "German",
        _ => "English"
    };

    private static AIRecommendationResult BuildRuleBasedRecommendations(
        string requirements,
        List<NormalizedVehicle> vehicles,
        string? language)
    {
        var query = (requirements ?? string.Empty).ToLowerInvariant();
        var filtered = vehicles.ToList();

        if (query.Contains("7") || query.Contains("seven") || query.Contains("large family"))
        {
            filtered = filtered.Where(v => v.Seats >= 7).ToList();
        }
        else if (query.Contains("5") || query.Contains("family") || query.Contains("group"))
        {
            filtered = filtered.Where(v => v.Seats >= 5).ToList();
        }
        else if (query.Contains("2") || query.Contains("couple"))
        {
            filtered = filtered.Where(v => v.Seats >= 2 && v.Seats <= 4).ToList();
        }

        if (query.Contains("luxury") || query.Contains("premium") || query.Contains("business"))
        {
            filtered = filtered.Where(v => v.Type.Equals("luxury", StringComparison.OrdinalIgnoreCase) || v.Type.Equals("sedan", StringComparison.OrdinalIgnoreCase)).ToList();
        }
        else if (query.Contains("suv") || query.Contains("off-road") || query.Contains("adventure"))
        {
            filtered = filtered.Where(v => v.Type.Equals("suv", StringComparison.OrdinalIgnoreCase)).ToList();
        }
        else if (query.Contains("economy") || query.Contains("budget") || query.Contains("cheap"))
        {
            filtered = filtered.Where(v => v.Type.Equals("economy", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (query.Contains("beach") || query.Contains("vacation"))
        {
            filtered = filtered.OrderByDescending(v => v.Seats).ThenBy(v => v.DailyRate ?? decimal.MaxValue).ToList();
        }
        else if (query.Contains("business") || query.Contains("meeting"))
        {
            filtered = filtered.Where(v => v.Type.Equals("luxury", StringComparison.OrdinalIgnoreCase) || v.Type.Equals("sedan", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (query.Contains("budget") || query.Contains("cheap") || query.Contains("affordable"))
        {
            filtered = filtered.OrderBy(v => v.DailyRate ?? decimal.MaxValue).ToList();
        }
        else
        {
            filtered = filtered.OrderByDescending(v => v.Seats).ThenBy(v => v.DailyRate ?? decimal.MaxValue).ToList();
        }

        if (filtered.Count == 0)
        {
            filtered = vehicles.OrderBy(v => v.DailyRate ?? decimal.MaxValue).Take(3).ToList();
        }

        var recommendations = filtered
            .Take(3)
            .Select((vehicle, index) =>
            {
                var matchScore = 70 + (filtered.Count - index) * 8;
                var reasoning = new StringBuilder();
                reasoning.Append("Ideal choice with ");
                reasoning.Append(vehicle.Seats);
                reasoning.Append(" seats, ");
                reasoning.Append(vehicle.Transmission);
                reasoning.Append(" transmission and ");
                reasoning.Append(vehicle.FuelType);
                reasoning.Append(" engine, perfect for your trip.");

                var pros = new List<string>();
                if ((vehicle.DailyRate ?? 0) > 0)
                {
                    pros.Add($"Competitive rate: {vehicle.DailyRate:C}");
                }
                if (!string.IsNullOrWhiteSpace(vehicle.Type))
                {
                    pros.Add($"Category: {vehicle.Type}");
                }
                pros.Add($"Seats up to {vehicle.Seats} passengers");

                var cons = new List<string>();
                if (query.Contains("budget") && (vehicle.DailyRate ?? 0) > 0 && vehicle.DailyRate > 75)
                {
                    cons.Add("Not the cheapest option available");
                }
                if (query.Contains("luxury") && !vehicle.Type.Equals("luxury", StringComparison.OrdinalIgnoreCase))
                {
                    cons.Add("Standard trim instead of luxury");
                }

                return new VehicleRecommendation
                {
                    VehicleId = vehicle.Id ?? string.Empty,
                    Make = vehicle.Make,
                    Model = vehicle.Model,
                    Rank = index + 1,
                    MatchScore = Math.Clamp(matchScore, 70, 99),
                    Reasoning = reasoning.ToString(),
                    Pros = pros,
                    Cons = cons,
                    TotalCost = vehicle.DailyRate,
                };
            })
            .ToList();

        var languagePrompts = GetLanguagePrompts(language ?? "en");

        var summary = new StringBuilder();
        summary.AppendLine(languagePrompts.RuleBasedSummary);
        foreach (var recommendation in recommendations)
        {
            summary.Append("#");
            summary.Append(recommendation.Rank);
            summary.Append(" - ");
            summary.Append(recommendation.Make);
            summary.Append(' ');
            summary.Append(recommendation.Model);
            if (recommendation.TotalCost.HasValue)
            {
                summary.Append(" (from ");
                summary.Append(recommendation.TotalCost.Value.ToString("C"));
                summary.Append(')');
            }
            summary.AppendLine();
        }

        return new AIRecommendationResult
        {
            Mode = "free",
            Summary = summary.ToString().Trim(),
            Recommendations = recommendations,
        };
    }

    private static NormalizedVehicle? NormalizeVehicle(VehicleData vehicle)
    {
        if (vehicle is null)
        {
            return null;
        }

        var id = vehicle.Id ?? vehicle.VehicleId;
        if (string.IsNullOrWhiteSpace(id))
        {
            id = Guid.NewGuid().ToString();
        }

        var dailyRate = vehicle.DailyRate ?? vehicle.DailyRateLegacy;

        return new NormalizedVehicle
        {
            Id = id,
            Make = vehicle.Make ?? string.Empty,
            Model = vehicle.Model ?? string.Empty,
            Year = vehicle.Year,
            Seats = vehicle.Seats ?? 0,
            DailyRate = dailyRate,
            Type = vehicle.Type ?? string.Empty,
            Transmission = vehicle.Transmission ?? string.Empty,
            FuelType = vehicle.FuelType ?? vehicle.FuelTypeLegacy ?? string.Empty,
            Features = vehicle.Features ?? new List<string>(),
        };
    }
}

public record LanguagePrompts(
    string SystemRole,
    string CustomerRequirements,
    string AvailableVehicles,
    string Instructions,
    string FallbackNotice,
    string RuleBasedSummary);

public class AIRecommendationRequest
{
    public string Requirements { get; set; } = string.Empty;

    public string Language { get; set; } = "en";

    public string Mode { get; set; } = "claude";

    public List<VehicleData> AvailableVehicles { get; set; } = new();
}

public class VehicleData
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("vehicle_id")]
    public string? VehicleId { get; set; }

    [JsonPropertyName("make")]
    public string? Make { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("dailyRate")]
    public decimal? DailyRate { get; set; }

    [JsonPropertyName("daily_rate")]
    public decimal? DailyRateLegacy { get; set; }

    [JsonPropertyName("seats")]
    public int? Seats { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("transmission")]
    public string? Transmission { get; set; }

    [JsonPropertyName("fuelType")]
    public string? FuelType { get; set; }

    [JsonPropertyName("fuel_type")]
    public string? FuelTypeLegacy { get; set; }

    [JsonPropertyName("features")]
    public List<string> Features { get; set; } = new();
}

public class AnthropicMessageRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 2048;

    [JsonPropertyName("temperature")]
    public decimal Temperature { get; set; } = 0.7m;

    [JsonPropertyName("messages")]
    public AnthropicMessage[] Messages { get; set; } = Array.Empty<AnthropicMessage>();
}

public class AnthropicMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public class ClaudeResponse
{
    [JsonPropertyName("content")]
    public List<ContentItem> Content { get; set; } = new();

    [JsonPropertyName("usage")]
    public Usage Usage { get; set; } = new();
}

public class ContentItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class Usage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
}

public class AIRecommendationResult
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "claude";

    [JsonPropertyName("recommendations")]
    public List<VehicleRecommendation> Recommendations { get; set; } = new();

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;
}

public class VehicleRecommendation
{
    [JsonPropertyName("vehicleId")]
    public string VehicleId { get; set; } = string.Empty;

    [JsonPropertyName("make")]
    public string Make { get; set; } = string.Empty;

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("rank")]
    public int Rank { get; set; }

    [JsonPropertyName("matchScore")]
    public int MatchScore { get; set; }

    [JsonPropertyName("reasoning")]
    public string Reasoning { get; set; } = string.Empty;

    [JsonPropertyName("pros")]
    public List<string> Pros { get; set; } = new();

    [JsonPropertyName("cons")]
    public List<string> Cons { get; set; } = new();

    [JsonPropertyName("totalCost")]
    public decimal? TotalCost { get; set; }
}

public class NormalizedVehicle
{
    public string? Id { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Seats { get; set; }
    public int? Year { get; set; }
    public decimal? DailyRate { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Transmission { get; set; } = string.Empty;
    public string FuelType { get; set; } = string.Empty;
    public List<string> Features { get; set; } = new();
}
