namespace Web_Sentro.Areas.Client.Models
{
    public class MitigationTaskViewModel
    {
        public int Id { get; set; }
        public int RiskId { get; set; }
        public string Title { get; set; } = "";
        public string AssignedTo { get; set; } = "";
        public string Priority { get; set; } = "";
        public string Status { get; set; } = "ToDo";
        public DateTime? DueDate { get; set; }
        public int ProgressPercent { get; set; }
    }
}