namespace POS.DTO
{
    public class VentasDTO
    {
        public int VentaId { get; set; }  // Identificador de la venta
        public DateTime Fecha { get; set; }  // Fecha de la venta
        public decimal Total { get; set; }  // Total de la venta
        public int ClienteId { get; set; }  // ID del cliente
        public string ClienteNombre { get; set; }  // Nombre del cliente
        public bool EsConsumidorFinal { get; set; } // Nueva propiedad

        public int EmpleadoId { get; set; }  // ID del empleado que registró la venta
        public string EmpleadoNombre { get; set; }  // Nombre del empleado
        public List<DetallesDTO> Detalles { get; set; }  // Detalles de la venta
    }
}
