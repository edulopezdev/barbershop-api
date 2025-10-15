using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using backend.Data;
using backend.Models;

namespace backend.Models
{
    public class Atencion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ClienteId { get; set; }

        [Required]
        public int BarberoId { get; set; }

        [Required]
        public DateTime Fecha { get; set; }

        [NotMapped]
        public decimal TotalCalculado => DetalleAtencion.Sum(d => d.Cantidad * d.PrecioUnitario);

        public int? TurnoId { get; set; }

        // Relación con CierreDiario
        public int? CierreDiarioId { get; set; }
        public CierreDiario? CierreDiario { get; set; }

        public Usuario Cliente { get; set; } = null!;
        public Usuario Barbero { get; set; } = null!;
        public ICollection<DetalleAtencion> DetalleAtencion { get; set; } =
            new List<DetalleAtencion>();
    }
}
