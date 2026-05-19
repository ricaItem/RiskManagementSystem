namespace WEB_Sentro.Services;

public static class AuditLogClassifier
{
    public const string Audit = "Audit";
    public const string System = "System";

    public static string NormalizeIp(string? ipAddress)
    {
        return string.IsNullOrWhiteSpace(ipAddress) ? "N/A" : ipAddress.Trim();
    }

    public static string DetermineCategory(string? entityType, string? actionType)
    {
        var entity = entityType?.Trim() ?? string.Empty;
        var action = actionType?.Trim() ?? string.Empty;

        if (entity.Equals("Identity", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Login", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Logout", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("2FA", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Lockout", StringComparison.OrdinalIgnoreCase))
        {
            return System;
        }

        if (action.StartsWith("Background", StringComparison.OrdinalIgnoreCase) ||
            action.StartsWith("Auto", StringComparison.OrdinalIgnoreCase) ||
            action.Equals("TenantLogUnavailable", StringComparison.OrdinalIgnoreCase) ||
            entity.Equals("MonitoringEvent", StringComparison.OrdinalIgnoreCase) ||
            entity.Equals("MonitoringAlert", StringComparison.OrdinalIgnoreCase) ||
            entity.Equals("Notification", StringComparison.OrdinalIgnoreCase))
        {
            return System;
        }

        return Audit;
    }
}
