namespace Web_Sentro.Areas.Client.Models
{
    public class RiskIdentificationViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Category { get; set; } // Operational, Financial, etc.
        public string Priority { get; set; } // Critical, Medium, Low
        public string DetectedBy { get; set; }
        public string ProjectSite { get; set; }
        public DateTime DateLogged { get; set; }
    }
}