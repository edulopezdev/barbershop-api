using System.ComponentModel.DataAnnotations;

namespace backend.Dtos
{
    public class ProductoServicioCrearDto
    {
        [Required]
        public string Nombre { get; set; }

        public string? Descripcion { get; set; }

        [Required]
        public decimal Precio { get; set; }

    public bool EsAlmacenable { get; set; }

        public int? Cantidad { get; set; }

        public IFormFile? Imagen { get; set; }

        public ProductoServicioCrearDto()
        {
            Nombre = string.Empty;
        }
    }
}
