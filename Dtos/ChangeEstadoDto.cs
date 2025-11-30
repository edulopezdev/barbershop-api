using System.ComponentModel.DataAnnotations;

namespace backend.Dtos
{
    public class ChangeEstadoDto
    {
        [Required]
        public int EstadoId { get; set; } // 2 = Confirmado, 3 = Cancelado
    }
}
