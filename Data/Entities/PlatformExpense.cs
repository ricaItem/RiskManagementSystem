using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WEB_Sentro.Data.Entities
{
    /// <summary>
    /// Represents expenses incurred by the platform itself (Super Admin expenses).
    /// </summary>
    public class PlatformExpense
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string Description { get; set; } = null!;

        [Required]
        public long AmountCentavos { get; set; }

        public string Currency { get; set; } = "PHP";

        [Required]
        public DateTime ExpenseDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [StringLength(450)]
        public string? CreatedByUserId { get; set; }
    }
}
