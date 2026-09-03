using System;
using System.Collections.Generic;

namespace LumenHut.Models;

public class TestRun
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;

    /// <summary>UTC. Converted to local time for display.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Free text the user attached to this run in the history page.</summary>
    public string? Notes { get; set; }

    // Conditions of the measurement. Without them two runs weeks apart cannot be compared:
    // a different engine version, machine or proxy path explains more than the numbers do.

    /// <summary>Operating system the run was measured on.</summary>
    public string? OsDescription { get; set; }

    /// <summary>Version of LumenHut that produced the run.</summary>
    public string? ToolVersion { get; set; }

    /// <summary>Browser window size used for the run, e.g. "1280x720".</summary>
    public string? Viewport { get; set; }

    /// <summary>Whether the run went through a configured proxy. Never the proxy itself.</summary>
    public bool ProxyUsed { get; set; }

    public List<BrowserResult> BrowserResults { get; set; } = new();
}
