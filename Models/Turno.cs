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

        // NUEVO: observación opcional que deja el cliente al cancelar (VARCHAR(500) NULL)
        public string? Observacion { get; set; }

        // NUEVAS PROPIEDADES
        public string? ModificadoPor { get; set; } // Quién modificó el turno
        public DateTime? FechaModificacion { get; set; } // Cuándo se modificó el turno

        // Propiedades de navegación
        public Usuario? Cliente { get; set; } // Relación con el cliente
        public Usuario? Barbero { get; set; } // Relación con el barbero
        public EstadoTurno? Estado { get; set; } // Relación con el estado del turno

        // Relación con la tabla Atencion
        public ICollection<Atencion> Atenciones { get; set; } = new List<Atencion>();
    }
}
