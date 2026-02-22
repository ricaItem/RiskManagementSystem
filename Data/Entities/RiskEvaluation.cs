namespace WEB_Sentro.Data.Entities
{
    public class RiskEvaluation
    {
        public int EvalId { get; set; }
        public int RiskId { get; set; }
        public string EvaluatedByUserId { get; set; } = null!;
        public int LikelihoodScore { get; set; }
        public int ImpactScore { get; set; }
        public int RiskScore { get; set; }
        public string RiskLevel { get; set; } = null!;
        public string? Decision { get; set; }
        public string? Remarks { get; set; }
        public DateTime EvaluatedAt { get; set; }

        public Risk Risk { get; set; } = null!;
    }
}
