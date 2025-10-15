namespace backend.Dtos
{
    public class CrearAtencionDto
    {
        public int ClienteId { get; set; }
        public DateTime? Fecha { get; set; }
        public List<DetalleAtencionDto> Detalles { get; set; } = new();
    }
}
