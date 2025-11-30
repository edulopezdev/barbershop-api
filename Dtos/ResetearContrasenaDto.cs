using System.ComponentModel.DataAnnotations;

namespace backend.Dtos
{
    public class ResetearContrasenaDto
    {
        [Required(ErrorMessage = "Token es requerido")]
        public string Token { get; set; } = null!;

        [Required(ErrorMessage = "Nueva contraseña es requerida")]
        [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres")]
        [StringLength(100)]
        public string NuevaContrasena { get; set; } = null!;

        [Required(ErrorMessage = "Confirmación de contraseña es requerida")]
        [Compare("NuevaContrasena", ErrorMessage = "Las contraseñas no coinciden")]
        public string ConfirmarContrasena { get; set; } = null!;
    }
}
