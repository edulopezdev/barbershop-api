using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Imagen
{
    [Key]
    public int IdImagen { get; set; }

    [Required]
    [MaxLength(255)]
    public string Ruta { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "varchar(50)")]
    public string TipoImagen { get; set; } = string.Empty;

    [Required]
    public int IdRelacionado { get; set; }

    [Required]
    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    [Required]
    public bool Activo { get; set; } = true;
}
