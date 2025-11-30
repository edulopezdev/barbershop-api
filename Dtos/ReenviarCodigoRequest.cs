using System.ComponentModel.DataAnnotations;

namespace backend.Dtos
{
    public class ReenviarCodigoRequest
    {
        [Required(ErrorMessage = "El email es requerido.")]
        [EmailAddress(ErrorMessage = "Email inválido.")]
        public string Email { get; set; } = null!;
    }
}
