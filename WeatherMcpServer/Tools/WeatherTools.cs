using System.ComponentModel;
using ModelContextProtocol.Server;
using Microsoft.Extensions.Logging;
using WeatherMcpServer.Services;

namespace WeatherMcpServer.Tools;

public class WeatherTools
{
	private readonly WeatherApiClient _api;
	private readonly ILogger<WeatherTools> _logger;

	public WeatherTools(WeatherApiClient api, ILogger<WeatherTools> logger)
	{
		_api = api;
		_logger = logger;
	}

	[McpServerTool]
	[Description("Gets current weather for a specified location using OpenWeatherMap.")]
	public async Task<WeatherApiClient.CurrentWeatherResult> GetCurrentWeather(
		[Description("City name, e.g. 'London'")] string city,
		[Description("Optional ISO 3166 country code, e.g. 'GB', 'US'")] string? countryCode = null,
		[Description("Units: 'metric', 'imperial', or 'standard'")] string units = "metric")
	{
		try
		{
			return await _api.GetCurrentWeatherAsync(city, countryCode, units, CancellationToken.None);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting current weather for {City} {Country}", city, countryCode);
			throw;
		}
	}

	[McpServerTool]
	[Description("Gets a multi-day weather forecast (at least 3 days) for a specified location.")]
	public async Task<WeatherApiClient.ForecastResult> GetWeatherForecast(
		[Description("City name, e.g. 'Tokyo'")] string city,
		[Description("Optional ISO 3166 country code, e.g. 'JP'")] string? countryCode = null,
		[Description("Units: 'metric', 'imperial', or 'standard'")] string units = "metric",
		[Description("Number of days to include (1-5). Defaults to 3.")] int days = 3)
	{
		try
		{
			if (days < 1) days = 1;
			if (days < 3) days = 3; // ensure at least 3-day forecast by default where possible
			if (days > 5) days = 5;
			return await _api.GetForecastAsync(city, countryCode, units, days, CancellationToken.None);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting forecast for {City} {Country}", city, countryCode);
			throw;
		}
	}

	[McpServerTool]
	[Description("Gets weather alerts/warnings for a specified location (if available).")]
	public async Task<WeatherApiClient.AlertsResult> GetWeatherAlerts(
		[Description("City name, e.g. 'New York'")] string city,
		[Description("Optional ISO 3166 country code, e.g. 'US'")] string? countryCode = null)
	{
		try
		{
			return await _api.GetAlertsAsync(city, countryCode, CancellationToken.None);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting alerts for {City} {Country}", city, countryCode);
			throw;
		}
	}
}