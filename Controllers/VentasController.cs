using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using POS.Data;
using POS.Models;
using System.Reflection.Metadata;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using POS.DTO;



namespace POS.Controllers
{
    public class VentasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public VentasController(ApplicationDbContext context)
        {
            _context = context;
        }


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


        public async Task<IActionResult> CrearVenta([FromBody] VentasDTO ventaDto)
        {
            if (ventaDto == null || ventaDto.Detalles == null || !ventaDto.Detalles.Any())
            {
                Console.WriteLine("El carrito llegó vacío o es nulo.");
                return BadRequest("Datos de la venta inválidos.");
            }

            // Verificación si el ClienteId existe en la base de datos
            var cliente = await _context.Clientes.FindAsync(ventaDto.ClienteId);
            if (cliente == null)
            {
                Console.WriteLine($"Cliente con ID {ventaDto.ClienteId} no encontrado.");
                return BadRequest($"El cliente con ID {ventaDto.ClienteId} no existe.");
            }

            // Obtener todos los productos necesarios antes de iniciar la transacción
            var productos = await _context.Productos
                                          .Where(p => ventaDto.Detalles.Select(d => d.ProductoId).Contains(p.Id))
                                          .ToListAsync();

            // Verificar si los productos existen y tienen suficiente stock
            foreach (var detalleDto in ventaDto.Detalles)
            {
                var producto = productos.FirstOrDefault(p => p.Id == detalleDto.ProductoId);
                if (producto == null)
                {
                    return BadRequest($"Producto con ID {detalleDto.ProductoId} no encontrado.");
                }

                if (producto.Stock < detalleDto.Cantidad)
                {
                    return BadRequest($"No hay suficiente stock para el producto {producto.Nombre}. Stock disponible: {producto.Stock}");
                }
            }

            // Crear la nueva venta
            Ventas nuevaVenta = new Ventas
            {
                Fecha = DateTime.Now,
                ClienteId = ventaDto.ClienteId,
                EmpleadoId = 1, // Suponiendo que el empleado es estático por ahora
                Total = ventaDto.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario)
            };

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Crear la venta
                    _context.Ventas.Add(nuevaVenta);
                    await _context.SaveChangesAsync();

                    foreach (DetallesDTO detalleDto in ventaDto.Detalles)
                    {
                        var detalle = new DetalleVenta
                        {
                            VentaId = nuevaVenta.VentaId,
                            ProductoId = detalleDto.ProductoId,
                            Cantidad = detalleDto.Cantidad,
                            PrecioUnitario = detalleDto.PrecioUnitario,
                            Subtotal = detalleDto.Cantidad * detalleDto.PrecioUnitario
                        };

                        _context.DetalleVentas.Add(detalle);

                        // Actualizar stock
                        var producto = productos.First(p => p.Id == detalleDto.ProductoId);
                        producto.Stock -= detalleDto.Cantidad;
                    }

                    // Guardar los cambios en Detalles y Stock actualizado
                    await _context.SaveChangesAsync();

                    // Commit de la transacción
                    await transaction.CommitAsync();
                    return Ok(new { VentaId = nuevaVenta.VentaId });
                }
                catch (Exception ex)
                {
                    // En caso de error, revertir cambios
                    await transaction.RollbackAsync();
                    return StatusCode(500, $"Hubo un problema al procesar la venta: {ex.Message}");
                }
            }
        }


    }
}

