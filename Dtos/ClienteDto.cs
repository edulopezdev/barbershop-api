namespace backend.Dtos
{
    public class ClienteDto
    {
        public int Id { get; set; }
        public string? Nombre { get; set; }
        public string? Email { get; set; }
        public string? Telefono { get; set; }
        public bool Activo { get; set; }
    }
}
