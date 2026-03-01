namespace WEB_Sentro.Data.Entities
{
    public class RiskMatrixCell
    {
        public int RiskMatrixCellId { get; set; }
        public int RiskMatrixConfigId { get; set; }
        public int Likelihood { get; set; }
        public int Impact { get; set; }
        public int Score { get; set; }

        public RiskMatrixConfig Config { get; set; } = null!;
    }
}
