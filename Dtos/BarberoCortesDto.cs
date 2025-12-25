using System.Collections.Generic;

namespace backend.Dtos
{
    public class ServicioCorteDto
    {
        public int ProductoServicioId { get; set; }
        public string? Nombre { get; set; }
        public int Cantidad { get; set; }
    }

    public class BarberoCortesMesDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int TotalCortes { get; set; }
        public List<ServicioCorteDto> PorServicio { get; set; } = new List<ServicioCorteDto>();
    }
}
