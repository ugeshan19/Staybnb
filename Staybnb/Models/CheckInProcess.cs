using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Staybnb.Models;

// Rules and required documents for one property; kept as JSON inside StepsJson
public class CheckInProcess
{
    public int Id { get; set; }

    public int PropertyId { get; set; }
    public HostProperty? Property { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = "Guest check-in";

    // serialised CheckInConfig
    public string StepsJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class CheckInConfig
{
    public string Instructions { get; set; } = string.Empty;
    public List<string> Rules { get; set; } = new();
    public List<string> RequiredDocuments { get; set; } = new();

    public static CheckInConfig Default() => new()
    {
        Instructions = "Welcome! Please complete the steps below before you arrive.",
        Rules = { "No smoking inside the property", "No parties or events", "Quiet hours 22:00 - 07:00", "Check-out by 10:00" },
        RequiredDocuments = { "South African ID", "Passport" }
    };

    public static CheckInConfig Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Default();
        try
        {
            var cfg = JsonSerializer.Deserialize<CheckInConfig>(json);
            if (cfg == null || (cfg.Rules.Count == 0 && cfg.RequiredDocuments.Count == 0 && string.IsNullOrEmpty(cfg.Instructions)))
                return Default();
            return cfg;
        }
        catch (JsonException)
        {
            return Default();
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this);
}
