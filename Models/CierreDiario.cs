using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace backend.Models
{
    public class CierreDiario
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime Fecha { get; set; }

        [Required]
        public decimal TotalProductosVendidos { get; set; }

        [Required]
        public decimal TotalServiciosVendidos { get; set; }

        [Required]
        public decimal TotalVentasDia { get; set; }

        public string? Observaciones { get; set; }

        [Required]
        public DateTime FechaCierre { get; set; } = DateTime.Now;

        [Required]
        public bool Cerrado { get; set; } = true;

        // Propiedad de navegación para los pagos relacionados
        public ICollection<CierreDiarioPago> Pagos { get; set; } = new List<CierreDiarioPago>();
        
        // Propiedad de navegación para las atenciones relacionadas
        public ICollection<Atencion> Atenciones { get; set; } = new List<Atencion>();
        
        public int UsuarioId { get; set; }
    }
}
