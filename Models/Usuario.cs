using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    public class Usuario
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string? Nombre { get; set; }

        [Required]
        [EmailAddress]
        public string? Email { get; set; }

        public string? Telefono { get; set; }

        public string? Avatar { get; set; }

        [Required]
        public int RolId { get; set; }

        public bool AccedeAlSistema { get; set; } = false;

        public bool Activo { get; set; } = true;

        public string? PasswordHash { get; set; } // Contraseña encriptada

        [NotMapped]
        public string? Password { get; set; } // Recibe la contraseña en texto plano, pero no se guarda en la DB

        public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

        public int IdUsuarioCrea { get; set; }

        public int? IdUsuarioModifica { get; set; }

        public DateTime? FechaModificacion { get; set; }

        [ForeignKey("RolId")]
        public Rol Rol { get; set; } = null!;
    }
}
