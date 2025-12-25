using System;
using System.Collections.Generic;

namespace backend.Dtos
{
    public class BarberoDashboardDto
    {
        public ProximoTurnoDto? ProximoTurno { get; set; }
        public ResumenBarberoDto Resumen { get; set; } = new ResumenBarberoDto();
        public List<UltimaAtencionDtoExtended> UltimasAtenciones { get; set; } =
            new List<UltimaAtencionDtoExtended>();
    }

    public class ResumenBarberoDto
    {
        public int TurnosHoy { get; set; }
        public int TurnosSemana { get; set; }
        public int TotalAtenciones { get; set; }
        public decimal Ingresos30Dias { get; set; }
    }

    public class ServicioCountDto
    {
        public int ProductoServicioId { get; set; }
        public string? Nombre { get; set; }
        public int Cantidad { get; set; }
    }

    public class UltimaAtencionDtoExtended
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public decimal Total { get; set; }
        public List<DetalleAtencionDto> Detalles { get; set; } = new List<DetalleAtencionDto>();
        public string? ClienteNombre { get; set; }
    }

    // Se usa `DetalleAtencionDto` definido en Dtos/DetalleAtencionDto.cs
}
