using System.ComponentModel.DataAnnotations;

namespace backend.Models
{
    public class BloqueoHorario
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BarberoId { get; set; }

        [Required]
        public DateTime FechaHoraInicio { get; set; }

        [Required]
        public DateTime FechaHoraFin { get; set; }

        public string? Motivo { get; set; }

        // Relación con Turno
        public ICollection<Turno> Turnos { get; set; } = new List<Turno>();
    }
}