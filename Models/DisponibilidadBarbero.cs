using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    [Table("disponibilidad_barbero")]
    public class DisponibilidadBarbero
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Usuario")]
        public int BarberoId { get; set; }

        [Required]
        [Range(0, 6)] // 0 = Domingo, 6 = Sábado
        public int DiaSemana { get; set; }

        [Required]
        public TimeSpan HoraInicio { get; set; }

        [Required]
        public TimeSpan HoraFin { get; set; }

        [Required]
        public bool Activo { get; set; }

        public Usuario Barbero { get; set; } = null!;
    }
}