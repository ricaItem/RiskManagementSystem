namespace WEB_Sentro.Areas.Vendor.Models;

public class OrgAdminsIndexViewModel
{
    public string? Search { get; set; }
    public int? OrganizationIdFilter { get; set; }
    public List<AdminRowViewModel> Admins { get; set; } = new();
    public List<OrganizationOptionViewModel> OrganizationOptions { get; set; } = new();
}

public class AdminRowViewModel
{
    public string UserId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string OrganizationName { get; set; } = null!;
    public int OrganizationId { get; set; }
    public string Role { get; set; } = "Admin";
    public string LastLoginDisplay { get; set; } = "—";
    public bool IsActive { get; set; }
}

public class OrganizationOptionViewModel
{
    public int OrganizationId { get; set; }
    public string OrgName { get; set; } = null!;
}
