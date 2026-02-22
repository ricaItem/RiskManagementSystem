using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WEB_Sentro.Data;
using WEB_Sentro.Data.Entities;

namespace WEB_Sentro.Services
{
    public class RiskAttachmentService
    {
        private const int MaxFiles = 5;
        private const int MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly RiskService _riskService;

        public RiskAttachmentService(ApplicationDbContext db, IWebHostEnvironment env, RiskService riskService)
        {
            _db = db;
            _env = env;
            _riskService = riskService;
        }

        public static bool IsAllowedExtension(string fileName)
        {
            var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
            return !string.IsNullOrEmpty(ext) && AllowedExtensions.Contains(ext);
        }

        public async Task<(bool Ok, string? Error)> SaveAttachmentsAsync(int riskId, int orgId, string userId, IEnumerable<IFormFile>? files, string? ipAddress, CancellationToken ct = default)
        {
            var risk = await _db.Risks.AsNoTracking().FirstOrDefaultAsync(r => r.RiskId == riskId && r.OrgId == orgId, ct);
            if (risk == null) return (false, "Risk not found");

            var fileList = files?.Where(f => f != null && f.Length > 0).ToList() ?? new List<IFormFile>();
            if (fileList.Count == 0) return (true, null);
            if (fileList.Count > MaxFiles) return (false, $"Maximum {MaxFiles} files allowed.");

            var uploadDir = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, "uploads", "risks", orgId.ToString(), riskId.ToString());
            Directory.CreateDirectory(uploadDir);

            var existingCount = await _db.Attachments.CountAsync(a => a.RiskId == riskId, ct);
            if (existingCount + fileList.Count > MaxFiles) return (false, $"Maximum {MaxFiles} files per risk.");

            foreach (var file in fileList)
            {
                if (file.Length == 0) continue;
                if (file.Length > MaxFileSizeBytes) return (false, $"File {file.FileName} exceeds 5MB.");
                if (!IsAllowedExtension(file.FileName)) return (false, $"File {file.FileName}: only jpg, jpeg, png, webp allowed.");

                var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? ".jpg";
                var safeName = $"{Guid.NewGuid():N}{ext}";
                var relativePath = Path.Combine("uploads", "risks", orgId.ToString(), riskId.ToString(), safeName).Replace('\\', '/');
                var fullPath = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, "uploads", "risks", orgId.ToString(), riskId.ToString(), safeName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                    await file.CopyToAsync(stream, ct);

                _db.Attachments.Add(new Attachment
                {
                    RiskId = riskId,
                    OrgId = orgId,
                    UploadedByUserId = userId,
                    FileName = file.FileName,
                    FileRef = "/" + relativePath,
                    UploadedAt = DateTime.UtcNow
                });
                _riskService.AddAuditLog(orgId, userId, "Attachment", riskId, "AttachmentUploaded", $"Uploaded {file.FileName}", ipAddress);
            }

            await _db.SaveChangesAsync(ct);
            return (true, null);
        }

        public async Task<bool> DeleteAttachmentAsync(int attachmentId, int? orgId, string userId, bool isAdmin, CancellationToken ct = default)
        {
            var q = _db.Attachments.Include(a => a.Risk).Where(a => a.AttachmentId == attachmentId);
            if (orgId.HasValue)
                q = q.Where(a => a.OrgId == orgId.Value);
            var att = await q.FirstOrDefaultAsync(ct);
            if (att == null) return false;
            if (!isAdmin && att.UploadedByUserId != userId) return false;

            var fullPath = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, att.FileRef!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(fullPath))
                try { System.IO.File.Delete(fullPath); } catch { }

            _db.Attachments.Remove(att);
            _riskService.AddAuditLog(att.OrgId, userId, "Attachment", att.AttachmentId, "AttachmentDeleted", $"Deleted {att.FileName}", null);
            await _db.SaveChangesAsync(ct);
            return true;
        }
    }
}
