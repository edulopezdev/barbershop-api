using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    public class DetalleAtencion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AtencionId { get; set; }

        [Required]
        public int ProductoServicioId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Cantidad { get; set; } = 1;

        [Required]
        [Range(0, 10000)]
        public decimal PrecioUnitario { get; set; }

        public string? Observacion { get; set; }

        // propiedades de navegación
        [ForeignKey("ProductoServicioId")]
        public ProductoServicio ProductoServicio { get; set; } = null!;
    }
}
