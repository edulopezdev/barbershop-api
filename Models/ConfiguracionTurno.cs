using System.ComponentModel.DataAnnotations;

namespace backend.Models
{
    public class ConfiguracionTurno
    {
        [Key]
        public int Id { get; set; }

        public int DuracionSlotMinutos { get; set; } = 30;

        public int MinutosAnticipacionCancelacion { get; set; } = 120;

        public int MaxTurnosPorCliente { get; set; } = 3;

        public int DiasAnticipacionMaxima { get; set; } = 30;
    }
}
