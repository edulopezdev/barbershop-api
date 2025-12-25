using System.ComponentModel.DataAnnotations;

namespace backend.Dtos
{
    public class ChangeEstadoDto
    {
        [Required]
        public int EstadoId { get; set; }

        /// <summary>
        /// Observación opcional para cambios de estado (especialmente útil para cancelaciones)
        /// </summary>
        public string? Observacion { get; set; }
    }
}
