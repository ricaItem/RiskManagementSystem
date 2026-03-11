using System.ComponentModel.DataAnnotations;

namespace WEB_Sentro.Data.Entities
{
    public class Incident
    {
        public int IncidentId { get; set; }
        public int OrgId { get; set; }
        public int SiteId { get; set; }
        public string ReportedByUserId { get; set; } = null!;
        
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = null!;
        
        [MaxLength(1000)]
        public string? Description { get; set; }
        
        public DateTime IncidentDate { get; set; }
        public DateTime ReportedAt { get; set; }
        
        [MaxLength(50)]
        public string Type { get; set; } = "Near Miss"; // Injury, Near Miss, Property Damage, Environmental, Other
        
        [MaxLength(20)]
        public string Severity { get; set; } = "Low"; // Critical, High, Medium, Low
        
        [MaxLength(20)]
        public string Status { get; set; } = "Open"; // Open, Investigating, Closed
        
        [MaxLength(1000)]
        public string? RootCause { get; set; }
        
        [MaxLength(1000)]
        public string? CorrectiveActions { get; set; }
        
        [MaxLength(100)]
        public string? WeatherConditions { get; set; }
        
        public DateTime UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }

        public Site Site { get; set; } = null!;
    }
}
