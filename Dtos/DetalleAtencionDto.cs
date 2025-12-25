using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Dtos
{
    public class DetalleAtencionDto
    {
        public int ProductoServicioId { get; set; }
        public int Cantidad { get; set; }

        // Nombre del producto/servicio (opcional, rellenado por consultas que lo incluyen)
        public string? Nombre { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal PrecioUnitario { get; set; }
        public string? Observacion { get; set; }
    }
}
