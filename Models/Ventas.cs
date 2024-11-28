using System.ComponentModel.DataAnnotations;

namespace POS.Models
{
    public class Ventas
    {
        [Key]
        public int VentaId { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;
        public int? ClienteId { get; set; }
        public int EmpleadoId { get; set; }
        public decimal Total { get; set; }

        // Relaciones
        public Cliente? Cliente { get; set; }
        public Empleado? Empleado { get; set; }
        public ICollection<DetalleVenta>? DetalleVentas { get; set; }
    }
}
