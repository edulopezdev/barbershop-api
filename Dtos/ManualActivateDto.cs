using System.ComponentModel.DataAnnotations;

namespace backend.Dtos
{
    public class ManualActivateDto
    {
        [Required]
        public int UsuarioId { get; set; }
    }
}
