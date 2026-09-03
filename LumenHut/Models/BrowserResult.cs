using System.Collections.Generic;

namespace LumenHut.Models;

public class BrowserResult
{
    public int Id { get; set; }
    public int TestRunId { get; set; }
    public TestRun? TestRun { get; set; }

    public string Browser { get; set; } = string.Empty; // Chromium, Firefox, WebKit

    /// <summary>Engine version reported by the browser, so a value stays interpretable later.</summary>
    public string? EngineVersion { get; set; }

    public bool Skipped { get; set; }
    public string? SkipReason { get; set; }

    public List<PerformanceMetric> Metrics { get; set; } = new();
}
