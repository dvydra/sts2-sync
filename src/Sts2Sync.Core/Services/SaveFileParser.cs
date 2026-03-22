using System.Text.Json;
using Sts2Sync.Core.Models;

namespace Sts2Sync.Core.Services;

public record ParseResult<T>(T? Value, string? Error) where T : class
{
    public bool IsSuccess => Error is null && Value is not null;
}

public static class SaveFileParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static ParseResult<ProgressSave> ParseProgressSave(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new ParseResult<ProgressSave>(null, "Empty input");

        if (!json.TrimStart().StartsWith('{'))
            return new ParseResult<ProgressSave>(null, "Invalid JSON: does not start with '{'");

        try
        {
            var result = JsonSerializer.Deserialize<ProgressSave>(json, Options);
            return result is null
                ? new ParseResult<ProgressSave>(null, "Deserialization returned null")
                : new ParseResult<ProgressSave>(result, null);
        }
        catch (JsonException ex)
        {
            return new ParseResult<ProgressSave>(null, $"JSON parse error: {ex.Message}");
        }
    }

    public static ParseResult<CurrentRunSave> ParseCurrentRun(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new ParseResult<CurrentRunSave>(null, "Empty input");

        if (!json.TrimStart().StartsWith('{'))
            return new ParseResult<CurrentRunSave>(null, "Invalid JSON: does not start with '{'");

        try
        {
            var result = JsonSerializer.Deserialize<CurrentRunSave>(json, Options);
            return result is null
                ? new ParseResult<CurrentRunSave>(null, "Deserialization returned null")
                : new ParseResult<CurrentRunSave>(result, null);
        }
        catch (JsonException ex)
        {
            return new ParseResult<CurrentRunSave>(null, $"JSON parse error: {ex.Message}");
        }
    }
}
