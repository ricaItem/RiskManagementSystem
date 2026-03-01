namespace Web_Sentro.Areas.Client.Models
{
    public class RiskMatrixViewModel
    {
        public int OrgId { get; set; }
        public int ConfigId { get; set; }
        public string Name { get; set; } = "";
        public List<RiskMatrixCellVm> Cells { get; set; } = new();
        public List<RiskAppetiteBandVm> Bands { get; set; } = new();
        public List<RiskTreatmentTriggerVm> Triggers { get; set; } = new();
    }

    public class RiskMatrixCellVm
    {
        public int Likelihood { get; set; }
        public int Impact { get; set; }
        public int Score { get; set; }
    }

    public class RiskAppetiteBandVm
    {
        public int MinScore { get; set; }
        public int MaxScore { get; set; }
        public string BandName { get; set; } = "";
        public int? ReviewFrequencyDays { get; set; }
    }

    public class RiskTreatmentTriggerVm
    {
        public string? BandName { get; set; }
        public bool RequiresJustification { get; set; }
        public List<string> AllowedDecisions { get; set; } = new();
    }
}
