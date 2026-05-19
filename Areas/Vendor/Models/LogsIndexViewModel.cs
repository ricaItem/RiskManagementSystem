namespace WEB_Sentro.Areas.Vendor.Models;

public class LogsIndexViewModel
{
    public string? Search { get; set; }
    public string? Severity { get; set; }
    public string? LogType { get; set; }
    public int? OrganizationId { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public List<OrganizationOptionViewModel> OrganizationOptions { get; set; } = new();
    public List<VendorLogRowViewModel> Logs { get; set; } = new();
}

public class VendorLogRowViewModel
{
    public DateTime TimestampUtc { get; set; }
    public string TimestampDisplay { get; set; } = string.Empty;
    public int OrganizationId { get; set; }
    public string OrganizationName { get; set; } = "-";
    public string ActorName { get; set; } = "-";
    public string IpAddress { get; set; } = "N/A";
    public string Category { get; set; } = "Audit";
    public string Event { get; set; } = "-";
    public string Status { get; set; } = "Info";
    public string StatusColorClass { get; set; } = "text-slate-500";
}
