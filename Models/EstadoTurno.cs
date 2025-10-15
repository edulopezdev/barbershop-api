using System.ComponentModel.DataAnnotations;

namespace backend.Models
{
    public class EstadoTurno
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string? Nombre { get; set; } // Nombre del estado (Ej: "Pendiente", "Confirmado", "Cancelado")
    }
}
