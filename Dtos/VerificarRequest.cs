using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace backend.Dtos
{
    public class VerificarRequest
    {
        [Required(ErrorMessage = "El código es requerido.")]
        [StringLength(4, MinimumLength = 4, ErrorMessage = "El código debe tener 4 dígitos.")]
        [JsonPropertyName("Code")]
        public string Code { get; set; } = null!;

        // OPCIONAL: solo si NO hay JWT
        [EmailAddress(ErrorMessage = "Email inválido.")]
        [JsonPropertyName("Email")]
        public string? Email { get; set; }
    }
}
