using System.ComponentModel.DataAnnotations;

namespace backend.Dtos
{
    public class OlvideContrasenaDto
    {
        [Required(ErrorMessage = "Email es requerido")]
        [EmailAddress(ErrorMessage = "Email con formato inválido")]
        [StringLength(200)]
        public string Email { get; set; } = null!;
    }
}
