using System.ComponentModel.DataAnnotations;

namespace backend.Models
{
    public class ConfiguracionSistema
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Clave { get; set; } = null!;

        [Required]
        [StringLength(500)]
        public string Valor { get; set; } = null!;

        [StringLength(200)]
        public string? Descripcion { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
        public DateTime? FechaModificacion { get; set; }
    }
}
