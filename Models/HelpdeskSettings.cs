namespace FaqCms.Models;

/// <summary>Single-row settings table for the public Helpdesk dashboard.
/// There is always exactly one row, with Id = 1.</summary>
public class HelpdeskSettings
{
    public int Id { get; set; } = 1;

    public string? DemoVideoUrl { get; set; }
    public string DemoVideoTitle { get; set; } = "Product Demo";

    public string PayrollSummaryTitle { get; set; } = "Payroll at a glance";
    public string PayrollSummaryBody { get; set; } =
        "A quick look at what our payroll module handles — pay runs, loans, adjustments, and reporting — all from one place.";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
