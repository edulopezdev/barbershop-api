using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    public class CierreDiarioPago
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CierreDiarioId { get; set; }

        [Required]
        [StringLength(50)]
        public string MetodoPagoNombre { get; set; } = null!;

        [Required]
        public decimal Monto { get; set; }

        // Propiedad de navegación hacia el cierre diario
        [ForeignKey("CierreDiarioId")]
        public CierreDiario? CierreDiario { get; set; }
    }
}
