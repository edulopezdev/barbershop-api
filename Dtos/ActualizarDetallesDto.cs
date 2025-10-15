using System.ComponentModel.DataAnnotations;

namespace backend.Dtos
{
    public class ActualizarDetallesDto
    {
        [Required]
        public List<DetalleDto> Detalles { get; set; } = new List<DetalleDto>();
    }

    public class DetalleDto
    {
        [Required]
        public int ProductoServicioId { get; set; }
        
        [Required]
        public int Cantidad { get; set; }
        
        [Required]
        public decimal PrecioUnitario { get; set; }
        
        public string? Observacion { get; set; }
    }
}