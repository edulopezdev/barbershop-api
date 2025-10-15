using System.ComponentModel.DataAnnotations;

namespace backend.Dtos
{
    public class CrearUsuarioDto : IValidatableObject
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [MaxLength(100, ErrorMessage = "El nombre no puede tener más de 100 caracteres.")]
        public string Nombre { get; set; } = null!;

        public string? Email { get; set; }

        public string? Telefono { get; set; }

        public string? Avatar { get; set; }

        [Required(ErrorMessage = "El RolId es obligatorio.")]
        public int RolId { get; set; }

        public bool AccedeAlSistema { get; set; } = false;

        public string? Password { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (AccedeAlSistema)
            {
                if (string.IsNullOrWhiteSpace(Email))
                {
                    yield return new ValidationResult(
                        "El email es obligatorio para usuarios que acceden al sistema.",
                        new[] { nameof(Email) }
                    );
                }
                else if (!new EmailAddressAttribute().IsValid(Email))
                {
                    yield return new ValidationResult(
                        "El email no tiene un formato válido.",
                        new[] { nameof(Email) }
                    );
                }

                if (string.IsNullOrWhiteSpace(Password))
                {
                    yield return new ValidationResult(
                        "La contraseña es obligatoria para usuarios que acceden al sistema.",
                        new[] { nameof(Password) }
                    );
                }
            }
            else if (!string.IsNullOrWhiteSpace(Email))
            {
                if (!new EmailAddressAttribute().IsValid(Email))
                {
                    yield return new ValidationResult(
                        "El email no tiene un formato válido.",
                        new[] { nameof(Email) }
                    );
                }
            }
        }
    }
}
