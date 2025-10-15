namespace backend.Dtos
{
    public class ProductoServicioVentaDto
    {
        public int Id { get; set; }
        public string? Nombre { get; set; } = string.Empty;
        public decimal? Precio { get; set; }
        public bool EsAlmacenable { get; set; }
    }
}
