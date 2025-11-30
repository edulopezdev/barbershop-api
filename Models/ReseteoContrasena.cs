using System;

namespace backend.Models
{
    public class ReseteoContrasena
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public byte[] TokenHash { get; set; } = null!;
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaExpiracion { get; set; }
        public bool Usado { get; set; } = false;
    }
}
