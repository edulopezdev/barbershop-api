namespace backend.Dtos
{
    public class DetalleVentaDto
    {
        public int ProductoServicioId { get; set; }
        public string? NombreProducto { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
        public string? Observacion { get; set; }
        public bool EsAlmacenable { get; set; } // Nuevo campo
    }
}
