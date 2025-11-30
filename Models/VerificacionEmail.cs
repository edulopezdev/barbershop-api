using System;

namespace backend.Models
{
    public class VerificacionEmail
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }

        // SHA-256 del código corto (4 dígitos)
        public byte[]? CodeHash { get; set; }

        public DateTime Expiracion { get; set; } // expiración general (24 horas)
        public DateTime? CodeExpiresAt { get; set; } // expiración del código (10 minutos)
        public DateTime FechaCreacion { get; set; }
        public bool Usado { get; set; }
        public int AttemptCount { get; set; } = 0;
    }
}
