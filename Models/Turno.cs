using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    public class Turno
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime FechaHora { get; set; }

        [Required]
        public int ClienteId { get; set; }

        [Required]
        public int BarberoId { get; set; }

        [Required]
        public int EstadoId { get; set; }

        // Relación con la tabla Atencion
        public ICollection<Atencion> Atenciones { get; set; } = new List<Atencion>();
    }
}
