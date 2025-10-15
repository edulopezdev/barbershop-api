using System.ComponentModel.DataAnnotations;

namespace backend.Dtos
{
    public class ProductoServicioConImagenDto
    {
        public int Id { get; set; }
        public string? Nombre { get; set; }
        public string? Descripcion { get; set; }
        public decimal? Precio { get; set; }
    public bool EsAlmacenable { get; set; }
        public int? Cantidad { get; set; }
        public IFormFile? Imagen { get; set; }
        public string? RutaImagen { get; set; }
        public bool Activo { get; set; } = true;
        public bool EliminarImagen { get; set; } = false;
    }
}
