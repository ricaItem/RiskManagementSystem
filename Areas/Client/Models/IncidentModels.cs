using System.ComponentModel.DataAnnotations;

namespace Web_Sentro.Areas.Client.Models
{
    public class IncidentViewModel
    {
        public int IncidentId { get; set; }
        public string Title { get; set; } = "";
        public int SiteId { get; set; } // Added for Edit Modal
        public string SiteName { get; set; } = "";
        public int? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public string? Description { get; set; } // Added for Edit Modal
        public DateTime IncidentDate { get; set; }
        public string Type { get; set; } = "";
        public string Severity { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime ReportedAt { get; set; }
    }

    public class IncidentEditViewModel
    {
        public int IncidentId { get; set; }
        
        [Required]
        public int? SiteId { get; set; }
        
        public int? ProjectId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Title { get; set; } = "";
        
        [MaxLength(250)]
        public string? Description { get; set; }
        
        [Required]
        public DateTime IncidentDate { get; set; } = DateTime.Now;
        
        [Required]
        public string Type { get; set; } = "Near Miss";
        
        [Required]
        public string Severity { get; set; } = "Low";
        
        public string Status { get; set; } = "Open";
        
        [MaxLength(250)]
        public string? RootCause { get; set; }
        
        [MaxLength(250)]
        public string? CorrectiveActions { get; set; }
        
        [MaxLength(100)]
        public string? WeatherConditions { get; set; }
    }
}
