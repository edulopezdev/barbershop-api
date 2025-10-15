using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace backend.Models
{
    public class Rol
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string NombreRol { get; set; } = null!; // no null permitido

        // Navegación inversa (opcional)
        public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    }
}
