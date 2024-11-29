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
using Document = QuestPDF.Fluent.Document;



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

        public IActionResult ImprimirFactura(long id)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            // Obtener la venta junto con los detalles
            var venta = _context.Ventas
                .Include(v => v.Cliente)
                
                .Include(v => v.DetalleVentas)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefault(v => v.VentaId == id);

            if (venta == null)
            {
                return NotFound();
            }

            var monedaGuatemala = new System.Globalization.CultureInfo("es-GT");

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);

                    page.Header().Row(header =>
                    {
                        header.RelativeItem().Text("Factura").FontSize(24).Bold().AlignCenter();
                    });

                    page.Content().Column(column =>
                    {
                        // Información de la venta
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"Fecha: {venta.Fecha.ToString("dd/MM/yyyy")}");
                            row.RelativeItem().Text($"No. Factura: {venta.VentaId}");
                        });

                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"Cliente: {venta.Cliente?.Nombre}");
                            row.RelativeItem().Text($"NIT: {venta.Cliente?.NIT}");
                        });


                        column.Item().PaddingVertical(1, Unit.Centimetre);

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.ConstantColumn(60);
                                columns.ConstantColumn(100);
                                columns.ConstantColumn(100);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(EstiloCelda).Text("Producto").FontSize(12).Bold();
                                header.Cell().Element(EstiloCelda).Text("Cantidad").FontSize(12).Bold();
                                header.Cell().Element(EstiloCelda).Text("Precio Unitario").FontSize(12).Bold();
                                header.Cell().Element(EstiloCelda).Text("Subtotal").FontSize(12).Bold();

                                static IContainer EstiloCelda(IContainer container)
                                {
                                    return container.Background("#e0e0e0").Border(1).BorderColor("#e0e0e0").Padding(5).AlignCenter();
                                }
                            });

                            // Agregar los detalles de la venta
                            foreach (DetalleVenta detalle in venta.DetalleVentas)
                            {
                                table.Cell().Border(1).BorderColor("#c0c0c0").Padding(5).Text(detalle.Producto?.Nombre ?? "N/A");
                                table.Cell().Border(1).BorderColor("#c0c0c0").Padding(5).AlignCenter().Text(detalle.Cantidad.ToString());
                                table.Cell().Border(1).BorderColor("#c0c0c0").Padding(5).AlignRight().Text(detalle.PrecioUnitario.ToString("C", monedaGuatemala));
                                table.Cell().Border(1).BorderColor("#c0c0c0").Padding(5).AlignRight().Text((detalle.Cantidad * detalle.PrecioUnitario).ToString("C", monedaGuatemala));
                            }

                            // Total de la venta
                            table.Cell().ColumnSpan(3).Background("#f0f0f0").Border(1).BorderColor("#c0c0c0").Padding(5).AlignRight()
                                .Text("TOTAL").FontSize(12).Bold();
                            table.Cell().Background("#f0f0f0").Border(1).BorderColor("#c0c0c0").Padding(5).AlignRight()
                                .Text(venta.Total.ToString("C", monedaGuatemala));
                        });
                    });

                    // Pie de página
                    page.Footer().Text($"USPG POS - 2024 - ").FontSize(10).AlignCenter();
                });
            });

            var stream = new MemoryStream();
            document.GeneratePdf(stream);
            stream.Position = 0;

            return File(stream, "application/pdf", $"Factura_{id}.pdf");
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

