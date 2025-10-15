using System.ComponentModel.DataAnnotations;

namespace backend.Dtos
{
    public class ActualizarPerfilDto
    {
        public string? Nombre { get; set; }
        public string? Email { get; set; }
        public string? Telefono { get; set; }
        public string? Password { get; set; }
    }
}
