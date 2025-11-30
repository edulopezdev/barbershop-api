using System.ComponentModel.DataAnnotations;

namespace backend.Dtos
{
    public class PublicRegistroDto
    {
        [Required]
        [StringLength(100)]
        public string Nombre { get; set; } = null!;

        [Required]
        [EmailAddress]
        [StringLength(200)]
        public string Email { get; set; } = null!;

        [Required]
        [MinLength(8)]
        [StringLength(100)]
        public string Password { get; set; } = null!;

        [Phone]
        [StringLength(30)]
        public string Telefono { get; set; } = null!;

        // Si vamos a integrar reCAPTCHA opcionalmente desde frontend
        public string RecaptchaToken { get; set; } = null!;
    }
}
