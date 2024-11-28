using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Data;
using POS.Models;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace POS.Controllers
{
    public class VentasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public VentasController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Index: Muestra una lista de productos con opciones de filtrado y búsqueda
        public IActionResult Index(string? search, string? filter)
        {
            var productos = _context.Productos.AsQueryable();

            // Filtrar por el tipo de búsqueda
            if (!string.IsNullOrEmpty(search) && !string.IsNullOrEmpty(filter))
            {
                switch (filter.ToLower())
                {
                    case "nombre":
                        productos = productos.Where(p => p.Nombre.Contains(search));
                        break;

                    case "codigo":
                        productos = productos.Where(p => p.Codigo.Contains(search));
                        break;

                    case "precio":
                        if (decimal.TryParse(search, out var precio))
                        {
                            productos = productos.Where(p => p.Precio == precio);
                        }
                        break;

                    default:
                        break;
                }
            }

            ViewBag.Clientes = _context.Clientes.ToList();
            var listaProductos = productos.ToList();
            return View(listaProductos);
        }

        // Verificar si una venta existe
        private bool VentaExists(int id)
        {
            return _context.Ventas.Any(e => e.VentaId == id);
        }

        [HttpPost]
        public async Task<IActionResult> CrearVenta(int clienteId, int empleadoId, List<int> productoIds, List<int> cantidades)
        {
            if (clienteId <= 0 || empleadoId <= 0 || productoIds == null || cantidades == null || productoIds.Count != cantidades.Count)
            {
                return BadRequest("Datos inválidos");
            }

            // Verificamos que el cliente exista
            var cliente = await _context.Clientes.FindAsync(clienteId);
            if (cliente == null)
            {
                return BadRequest("El cliente seleccionado no existe");
            }

            // Verificamos que el empleado exista
            var empleado = await _context.Empleados.FindAsync(empleadoId);
            if (empleado == null)
            {
                return BadRequest("El empleado seleccionado no existe");
            }

            // Crear la venta
            var nuevaVenta = new Ventas
            {
                Fecha = DateTime.Now,
                ClienteId = clienteId,
                EmpleadoId = empleadoId,
                Total = 0 // Se calculará más adelante
            };

            _context.Ventas.Add(nuevaVenta);
            await _context.SaveChangesAsync();

            decimal totalVenta = 0;

            // Procesar los detalles de la venta
            for (int i = 0; i < productoIds.Count; i++)
            {
                var productoId = productoIds[i];
                var cantidad = cantidades[i];

                var producto = await _context.Productos.FindAsync(productoId);
                if (producto == null)
                {
                    return BadRequest($"El producto con ID {productoId} no existe.");
                }

                // Verificar que haya stock suficiente
                if (producto.Stock < cantidad)
                {
                    return BadRequest($"No hay suficiente stock para el producto {producto.Nombre}. Stock disponible: {producto.Stock}");
                }

                // Crear detalle de venta
                var detalle = new DetalleVenta
                {
                    VentaId = nuevaVenta.VentaId,
                    ProductoId = productoId,
                    Cantidad = cantidad,
                    PrecioUnitario = producto.Precio,
                    Subtotal = cantidad * producto.Precio
                };

                _context.DetalleVentas.Add(detalle);

                // Actualizar el stock del producto
                producto.Stock -= cantidad;
                totalVenta += detalle.Subtotal;
            }

            // Actualizar el total de la venta
            nuevaVenta.Total = totalVenta;

            await _context.SaveChangesAsync();

            return Ok(new { VentaId = nuevaVenta.VentaId });
        }
    }
}
