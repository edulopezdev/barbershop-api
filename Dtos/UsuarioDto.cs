public class UsuarioDto
{
    public int Id { get; set; }
    public string? Nombre { get; set; }
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public string? Avatar { get; set; }
    public int RolId { get; set; }
    public bool AccedeAlSistema { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaRegistro { get; set; }
    public DateTime? FechaModificacion { get; set; }
    public string RolNombre { get; set; } = string.Empty;
}
