namespace POS.DTO
{
    public class DetallesDTO
    {
        public int DetalleVentaId { get; set; }  // Identificador del detalle de la venta
        public int ProductoId { get; set; }  // ID del producto
        public string ProductoNombre { get; set; }  // Nombre del producto
        public int Cantidad { get; set; }  // Cantidad del producto
        public decimal PrecioUnitario { get; set; }  // Precio unitario del producto
        public decimal Subtotal { get; set; }  // Subtotal (Cantidad * PrecioUnitario)
    }
}
