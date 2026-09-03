namespace LumenHut.Models;

public class PerformanceMetric
{
    public int Id { get; set; }
    public int BrowserResultId { get; set; }
    public BrowserResult? BrowserResult { get; set; }

    public string Name { get; set; } = string.Empty; // LCP, CLS, FCP, LoadTime, TTFB, INP
    public double? Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Note { get; set; } // e.g. "N/A - not supported in this engine" or measurement notes
}
