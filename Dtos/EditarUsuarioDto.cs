using System.ComponentModel.DataAnnotations;

namespace backend.Dtos
{
    public class EditarUsuarioDto
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [MaxLength(100, ErrorMessage = "El nombre no puede tener más de 100 caracteres.")]
        public string Nombre { get; set; } = null!;

        [Required(ErrorMessage = "El email es obligatorio.")]
        [EmailAddress(ErrorMessage = "Formato de email inválido.")]
        public string Email { get; set; } = null!;

        public string? Telefono { get; set; }

        public string? Avatar { get; set; }

        public int RolId { get; set; }

        public bool AccedeAlSistema { get; set; }

        public bool? Activo { get; set; }

        public string? Password { get; set; }
    }
}
