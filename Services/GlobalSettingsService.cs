using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;

namespace WEB_Sentro.Services;

public interface IGlobalSettingsService
{
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, string? updatedByUserId, CancellationToken ct = default) where T : class;
}

public class GlobalSettingsService : IGlobalSettingsService
{
    private readonly PlatformDbContext _db;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public GlobalSettingsService(PlatformDbContext db)
    {
        _db = db;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        return _db.PlatformSettings.AsNoTracking().AnyAsync(x => x.Key == key, ct);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        var json = await _db.PlatformSettings.AsNoTracking()
            .Where(x => x.Key == key)
            .Select(x => x.JsonValue)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    public async Task SetAsync<T>(string key, T value, string? updatedByUserId, CancellationToken ct = default) where T : class
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        var existing = await _db.PlatformSettings.FirstOrDefaultAsync(x => x.Key == key, ct);

        if (existing == null)
        {
            _db.PlatformSettings.Add(new PlatformSetting
            {
                Key = key,
                JsonValue = json,
                UpdatedAt = DateTime.UtcNow,
                UpdatedByUserId = updatedByUserId
            });
        }
        else
        {
            existing.JsonValue = json;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedByUserId = updatedByUserId;
        }

        await _db.SaveChangesAsync(ct);
    }
}
