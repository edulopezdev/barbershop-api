using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq; // ✅ Necesario para usar .Sum()
using backend.Models;

public enum MetodoPago
{
    Efectivo,
    TarjetaDebito,
    TarjetaCredito,
    Transferencia,
    MercadoPago,
    NaranjaX,
    QR,
    Otro,
}

public class Pago
{
    [Key]
    public int Id { get; set; }

    [Required]
    [ForeignKey("Atencion")]
    public int AtencionId { get; set; }

    [Required]
    public MetodoPago MetodoPago { get; set; }

    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal Monto { get; set; }

    [Required]
    public DateTime Fecha { get; set; } = DateTime.Now;

    public virtual Atencion? Atencion { get; set; }

    public List<MetodoPagoDetalle> Metodos { get; set; } = new();

    [NotMapped]
    public decimal MontoTotal => Metodos?.Sum(m => m.Monto) ?? 0;
}
