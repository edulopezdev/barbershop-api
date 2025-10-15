public class MetodoPagoDetalle
{
    public int Id { get; set; }
    public int PagoId { get; set; }
    public MetodoPago Metodo { get; set; }
    public decimal Monto { get; set; }
}
