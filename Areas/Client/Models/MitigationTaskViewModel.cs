namespace Web_Sentro.Areas.Client.Models
{
    public class MitigationTaskViewModel
    {
        public string Id { get; set; }
        public int RiskId { get; set; }
        public string Title { get; set; }
        public string AssignedTo { get; set; }
        public string Priority { get; set; } // Critical, High, Medium
        public string Status { get; set; } // ToDo, InProgress, Review, Done
        public DateTime DueDate { get; set; }
    }
}