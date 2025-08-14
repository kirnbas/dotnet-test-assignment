# Real Weather MCP Server - Test Assignment

This is a test assignment for **[FastMCP.me](https://fastmcp.me)** - a service for creating and deploying MCP servers.

You are tasked with creating a **Real Weather MCP Server** using the new .NET MCP (Model Context Protocol) library.

**Note**: The use of AI development tools like Cursor, Claude Code, GitHub Copilot, and other AI IDEs is welcomed and encouraged for this assignment.

## Assignment Overview

Your task is to create a functional MCP server that provides real weather data through AI assistants like Claude. The server should integrate with actual weather APIs and provide accurate, current weather information.

## Requirements

### Core Functionality
- **Real Weather Data**: Integrate with a real weather API (e.g., OpenWeatherMap, AccuWeather, or similar)
- **Current Weather**: Get current weather conditions for any city/location
- **Weather Forecast**: Provide weather forecasts (at least 3-day forecast)
- **Multiple Locations**: Support weather queries for different cities worldwide
- **Error Handling**: Proper error handling for invalid locations, API failures, etc.

### Technical Requirements
- Use the .NET MCP Server library (`Microsoft.Extensions.AI.Abstractions`)
- Implement proper MCP tools using `[McpServerTool]` attributes
- Include environment variable configuration for API keys
- Follow .NET best practices and coding standards
- Include proper logging and error handling

### Expected Tools to Implement
1. `GetCurrentWeather` - Get current weather for a specified location
2. `GetWeatherForecast` - Get weather forecast for a specified location
3. `GetWeatherAlerts` - Get weather alerts/warnings for a location (bonus)

## Getting Started

### Prerequisites
- .NET 8.0 or later
- Weather API key (recommend OpenWeatherMap free tier)

### Setup Instructions
1. Install the MCP server template:
   ```bash
   dotnet new install Microsoft.Extensions.AI.Templates
   ```

2. The basic project structure is already provided in the `WeatherMcpServer` directory

3. Get a free API key from a weather service provider

4. Configure your API key as an environment variable

   - On Windows (PowerShell):
     ```powershell
     setx OPENWEATHER_API_KEY "<your_api_key_here>"
     ```
   - On macOS/Linux (bash):
     ```bash
     export OPENWEATHER_API_KEY="<your_api_key_here>"
     ```
   - The server reads `OPENWEATHER_API_KEY` (or falls back to `WEATHER_API_KEY`).

### Example Tool Structure
```csharp
[McpServerTool]
[Description("Gets current weather conditions for the specified city.")]
public async Task<string> GetCurrentWeather(
    [Description("The city name to get weather for")] string city,
    [Description("Optional: Country code (e.g., 'US', 'UK')")] string? countryCode = null)
{
    // Your implementation here
}
```

### Implemented Tools

- `GetCurrentWeather(city, countryCode?, units?)`: Returns structured current conditions using OpenWeatherMap.
- `GetWeatherForecast(city, countryCode?, units?, days?)`: Returns at least a 3-day forecast summarized per day.
- `GetWeatherAlerts(city, countryCode?)`: Returns active alerts if available; otherwise an empty list.

### Units

Supported units are `metric`, `imperial`, and `standard`. Defaults to `metric`.

## Evaluation Criteria

Your solution will be evaluated on:

1. **Functionality** (40%)
   - Does it work with real weather data?
   - Are all required features implemented?
   - How well does it handle edge cases?

2. **Code Quality** (30%)
   - Clean, readable, and maintainable code
   - Proper error handling and logging
   - Following .NET conventions

3. **MCP Integration** (20%)
   - Proper use of MCP server attributes and patterns
   - Good tool descriptions and parameter definitions
   - Correct server configuration

4. **Documentation & Testing** (10%)
   - Clear documentation of setup and usage
   - Basic testing of functionality
   - API key configuration instructions

## Resources

- [MCP .NET Documentation](https://learn.microsoft.com/dotnet/ai/quickstarts/build-mcp-server)
- [MCP .NET Samples](https://github.com/microsoft/mcp-dotnet-samples)
- [MCP Server Quickstart Blog](https://devblogs.microsoft.com/dotnet/mcp-server-dotnet-nuget-quickstart/)
- [OpenWeatherMap API](https://openweathermap.org/api) (free tier available)

## Submission

Please provide:
1. Complete source code with proper project structure
2. Instructions for setup and configuration
3. Example usage or demo of the working server
4. Brief documentation of your implementation approach

## Time Expectation

This assignment should take approximately 2-4 hours to complete, depending on your experience level.

Good luck! 🌤️