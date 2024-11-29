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

        [HttpPost]
        public async Task<IActionResult> CrearVenta([FromBody] VentasDTO ventaDto)
        {
            if (ventaDto == null || ventaDto.Detalles == null || !ventaDto.Detalles.Any())
            {
                return BadRequest("Datos de la venta inválidos");
            }

            // Verificar si el cliente existe
            var cliente = await _context.Clientes.FindAsync(ventaDto.ClienteId);
            if (cliente == null)
            {
                return BadRequest("El cliente seleccionado no existe");
            }

            // Crear la venta
            Ventas nuevaVenta = new Ventas
            {
                Fecha = DateTime.Now,
                ClienteId = ventaDto.ClienteId,
                EmpleadoId = 1, // Suponiendo que el empleado es estático por ahora
                Total = ventaDto.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario),
            };
            try
            {
                _context.Ventas.Add(nuevaVenta);
                await _context.SaveChangesAsync();

                foreach (var detalleDto in ventaDto.Detalles)
                {
                    var detalle = new DetalleVenta
                    {
                        VentaId = nuevaVenta.VentaId,
                        ProductoId = detalleDto.ProductoId,
                        Cantidad = detalleDto.Cantidad,
                        PrecioUnitario = detalleDto.PrecioUnitario,
                        Subtotal = detalleDto.Cantidad * detalleDto.PrecioUnitario
                    };
                    _context.DetallesVentas.Add(detalle);

                    var producto = await _context.Productos.FindAsync(detalleDto.ProductoId);
                    if (producto != null)
                    {
                        // Restar la cantidad vendida del stock
                        if (producto.Stock >= detalleDto.Cantidad)
                        {
                            producto.Stock -= detalleDto.Cantidad;
                        }
                        else
                        {
                            return BadRequest($"No hay suficiente stock para el producto {producto.Nombre}");
                        }
                    }
                    else
                    {
                        return BadRequest($"El producto con ID {detalleDto.ProductoId} no existe");
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new { VentaId = nuevaVenta.VentaId });
            }
            catch (Exception ex)
            {
                
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace, innerException = ex.InnerException?.Message });
            }
        } 


    }    
}

