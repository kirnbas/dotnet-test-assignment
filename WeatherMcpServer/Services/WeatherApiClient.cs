using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace WeatherMcpServer.Services;

public sealed class WeatherApiClient
{
	private readonly HttpClient _httpClient;
	private readonly ILogger<WeatherApiClient> _logger;
	private readonly string _apiKey;

	public WeatherApiClient(IHttpClientFactory httpClientFactory, ILogger<WeatherApiClient> logger)
	{
		_httpClient = httpClientFactory.CreateClient();
		_logger = logger;
		_apiKey = Environment.GetEnvironmentVariable("OPENWEATHER_API_KEY")
			?? Environment.GetEnvironmentVariable("WEATHER_API_KEY")
			?? string.Empty;

		if (string.IsNullOrWhiteSpace(_apiKey))
		{
			throw new InvalidOperationException("Missing OpenWeatherMap API key. Set OPENWEATHER_API_KEY or WEATHER_API_KEY environment variable.");
		}
	}

	public sealed record GeocodeResult(string name, double lat, double lon, string? country, string? state);
	public sealed record CurrentWeatherResult(
		string locationName,
		string? countryCode,
		string units,
		double temperature,
		string conditions,
		double humidity,
		double windSpeed,
		double? windGust,
		double? pressure,
		double? cloudiness,
		double? visibilityMeters
	);

	public sealed record DailyForecast(DateOnly date, string units, double minTemp, double maxTemp, string summary);
	public sealed record ForecastResult(string locationName, string? countryCode, string units, IReadOnlyList<DailyForecast> days);
	public sealed record WeatherAlert(string sender, string eventName, string description, DateTimeOffset start, DateTimeOffset end, string? tags);
	public sealed record AlertsResult(string locationName, string? countryCode, IReadOnlyList<WeatherAlert> alerts);

	public async Task<GeocodeResult> GeocodeAsync(string city, string? countryCode, CancellationToken cancellationToken)
	{
		var q = string.IsNullOrWhiteSpace(countryCode) ? city : $"{city},{countryCode}";
		var url = $"https://api.openweathermap.org/geo/1.0/direct?q={Uri.EscapeDataString(q)}&limit=1&appid={_apiKey}";
		var response = await _httpClient.GetAsync(url, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			var body = await response.Content.ReadAsStringAsync(cancellationToken);
			_logger.LogWarning("Geocoding API failed: {Status} {Body}", (int)response.StatusCode, body);
			throw new InvalidOperationException("Failed to geocode the requested location.");
		}

		var results = await response.Content.ReadFromJsonAsync<List<GeocodeResult>>(cancellationToken: cancellationToken)
			?? new List<GeocodeResult>();
		var first = results.FirstOrDefault();
		if (first is null)
		{
			throw new InvalidOperationException("Location not found. Please provide a valid city name and optional country code.");
		}
		return first;
	}

	public async Task<CurrentWeatherResult> GetCurrentWeatherAsync(string city, string? countryCode, string units, CancellationToken cancellationToken)
	{
		ValidateUnits(units);
		var geo = await GeocodeAsync(city, countryCode, cancellationToken);
		var url = $"https://api.openweathermap.org/data/2.5/weather?lat={geo.lat}&lon={geo.lon}&appid={_apiKey}&units={units}";
		var response = await _httpClient.GetAsync(url, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			var body = await response.Content.ReadAsStringAsync(cancellationToken);
			_logger.LogWarning("Current weather API failed: {Status} {Body}", (int)response.StatusCode, body);
			throw new InvalidOperationException("Failed to retrieve current weather.");
		}

		var json = await response.Content.ReadFromJsonAsync<JsonNode?>(cancellationToken: cancellationToken);
		if (json is null)
		{
			throw new InvalidOperationException("Unexpected response from weather service.");
		}

		var main = json["main"];
		double temperature = main?["temp"]?.GetValue<double>() ?? double.NaN;
		double humidity = main?["humidity"]?.GetValue<double>() ?? double.NaN;
		double? pressure = main?["pressure"]?.GetValue<double>();

		var wind = json["wind"];
		double windSpeed = wind?["speed"]?.GetValue<double>() ?? double.NaN;
		double? windGust = wind?["gust"]?.GetValue<double>();

		double? cloudiness = json["clouds"]?["all"]?.GetValue<double>();
		double? visibility = json["visibility"]?.GetValue<double>();

		var weatherArr = json["weather"]?.AsArray();
		string conditions = (weatherArr is { Count: > 0 } && weatherArr[0]?["description"] != null)
			? weatherArr[0]!["description"]!.GetValue<string>()
			: "unknown";

		return new CurrentWeatherResult(
			geo.name,
			geo.country,
			units,
			temperature,
			conditions,
			humidity,
			windSpeed,
			windGust,
			pressure,
			cloudiness,
			visibility
		);
	}

	public async Task<ForecastResult> GetForecastAsync(string city, string? countryCode, string units, int days, CancellationToken cancellationToken)
	{
		ValidateUnits(units);
		if (days < 1) days = 1;
		if (days > 5) days = 5;

		var geo = await GeocodeAsync(city, countryCode, cancellationToken);
		var url = $"https://api.openweathermap.org/data/2.5/forecast?lat={geo.lat}&lon={geo.lon}&appid={_apiKey}&units={units}";
		var response = await _httpClient.GetAsync(url, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			var body = await response.Content.ReadAsStringAsync(cancellationToken);
			_logger.LogWarning("Forecast API failed: {Status} {Body}", (int)response.StatusCode, body);
			throw new InvalidOperationException("Failed to retrieve weather forecast.");
		}

		var json = await response.Content.ReadFromJsonAsync<JsonNode?>(cancellationToken: cancellationToken);
		if (json is null)
		{
			throw new InvalidOperationException("Unexpected response from forecast service.");
		}

		var groups = new Dictionary<DateOnly, List<JsonNode>>();
		var listNode = json["list"]?.AsArray();
		if (listNode is null)
		{
			throw new InvalidOperationException("Forecast list missing from response.");
		}

		foreach (var item in listNode)
		{
			if (item is null) continue;
			long dt = item["dt"]?.GetValue<long>() ?? 0;
			var date = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(dt).Date);
			if (!groups.TryGetValue(date, out var bucket))
			{
				bucket = new List<JsonNode>();
				groups[date] = bucket;
			}
			bucket.Add(item);
		}

		var ordered = groups.Keys.OrderBy(d => d).Take(days);
		var resultDays = new List<DailyForecast>();
		foreach (var day in ordered)
		{
			var bucket = groups[day];
			double min = double.MaxValue;
			double max = double.MinValue;
			var summaries = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			foreach (var p in bucket)
			{
				double tMin = p["main"]?["temp_min"]?.GetValue<double>() ?? double.NaN;
				double tMax = p["main"]?["temp_max"]?.GetValue<double>() ?? double.NaN;
				if (tMin < min) min = tMin;
				if (tMax > max) max = tMax;
				var wArr = p["weather"]?.AsArray();
				string summary = (wArr is { Count: > 0 } && wArr[0]?["description"] != null)
					? wArr[0]!["description"]!.GetValue<string>()
					: "unknown";
				if (!summaries.ContainsKey(summary)) summaries[summary] = 0;
				summaries[summary]++;
			}
			string summaryFinal = summaries.OrderByDescending(kv => kv.Value).First().Key;
			resultDays.Add(new DailyForecast(day, units, min, max, summaryFinal));
		}

		return new ForecastResult(geo.name, geo.country, units, resultDays);
	}

	public async Task<AlertsResult> GetAlertsAsync(string city, string? countryCode, CancellationToken cancellationToken)
	{
		var geo = await GeocodeAsync(city, countryCode, cancellationToken);
		var url = $"https://api.openweathermap.org/data/3.0/onecall?lat={geo.lat}&lon={geo.lon}&appid={_apiKey}&exclude=minutely,hourly,daily,current";
		var response = await _httpClient.GetAsync(url, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			var body = await response.Content.ReadAsStringAsync(cancellationToken);
			_logger.LogWarning("Alerts API failed: {Status} {Body}", (int)response.StatusCode, body);
			// Do not fail hard; return empty alerts if unsupported
			return new AlertsResult(geo.name, geo.country, Array.Empty<WeatherAlert>());
		}

		var json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode?>(cancellationToken: cancellationToken);
		var alertsArr = json?["alerts"]?.AsArray();
		var result = new List<WeatherAlert>();
		if (alertsArr is { Count: > 0 })
		{
			foreach (var a in alertsArr)
			{
				if (a is null) continue;
				string sender = a["sender_name"]?.GetValue<string>() ?? "Unknown";
				string eventName = a["event"]?.GetValue<string>() ?? "Alert";
				string description = a["description"]?.GetValue<string>() ?? string.Empty;
				long start = a["start"]?.GetValue<long>() ?? 0;
				long end = a["end"]?.GetValue<long>() ?? 0;
				string? tags = null;
				var tagsArr = a["tags"]?.AsArray();
				if (tagsArr is { Count: > 0 })
				{
					tags = string.Join(", ", tagsArr.Select(t => t?.GetValue<string>()).Where(s => !string.IsNullOrWhiteSpace(s))!);
				}
				result.Add(new WeatherAlert(
					sender,
					eventName,
					description,
					DateTimeOffset.FromUnixTimeSeconds(start),
					DateTimeOffset.FromUnixTimeSeconds(end),
					tags
				));
			}
		}

		return new AlertsResult(geo.name, geo.country, result);
	}

	private static void ValidateUnits(string units)
	{
		var allowed = new[] { "standard", "metric", "imperial" };
		if (!allowed.Contains(units, StringComparer.OrdinalIgnoreCase))
		{
			throw new ArgumentException("Units must be one of: standard, metric, imperial", nameof(units));
		}
	}
}


