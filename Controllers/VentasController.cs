// Controllers/VentasController.cs
using Microsoft.AspNetCore.Mvc;
using TodoApi.Services;

namespace TodoApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VentasController : ControllerBase
    {
        private readonly GoogleSheetsService _googleSheetsService;

        public VentasController(GoogleSheetsService googleSheetsService)
        {
            _googleSheetsService = googleSheetsService;
        }

        [HttpGet]
        public async Task<ActionResult<VentasData>> GetVentasData()
        {
            try
            {
                var data = await _googleSheetsService.GetVentasDataAsync();
                
                // Redondear valores NAP en los totales
                foreach (var total in data.Totales)
                {
                    total.Value.Nap = Math.Round(total.Value.Nap, 0);
                }
                
                return Ok(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    error = "Error al obtener datos de Google Sheets", 
                    message = ex.Message 
                });
            }
        }

        [HttpGet("resumen")]
        public async Task<ActionResult<object>> GetResumen()
        {
            try
            {
                var data = await _googleSheetsService.GetVentasDataAsync();
                
                var totalVentas = data.Totales.Values.Sum(m => m.Ventas);
                var totalNap = data.Totales.Values.Sum(m => m.Nap);
                
                var resumen = new
                {
                    TotalVentas = totalVentas,
                    TotalNap = Math.Round(totalNap, 0),
                    PromedioMensualVentas = Math.Round((double)totalVentas / data.Totales.Count, 1),
                    PromedioMensualNap = Math.Round(totalNap / data.Totales.Count, 0),
                    MesesProcesados = data.Totales.Count,
                    UltimaActualizacion = DateTime.Now
                };

                return Ok(resumen);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    error = "Error al obtener resumen", 
                    message = ex.Message 
                });
            }
        }

        [HttpGet("tendencia")]
        public async Task<ActionResult<object>> GetTendencia()
        {
            try
            {
                var data = await _googleSheetsService.GetVentasDataAsync();
                
                var meses = new[] { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio" };
                var nombresMeses = new[] { "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul" };
                
                var tendencia = new
                {
                    Labels = nombresMeses,
                    Ventas = meses.Select(m => data.Totales.GetValueOrDefault(m, new VentasMes()).Ventas).ToArray(),
                    Nap = meses.Select(m => Math.Round(data.Totales.GetValueOrDefault(m, new VentasMes()).Nap, 0)).ToArray()
                };

                return Ok(tendencia);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    error = "Error al obtener tendencia", 
                    message = ex.Message 
                });
            }
        }

        [HttpGet("coordinadores")]
        public async Task<ActionResult<object>> GetCoordinadores()
        {
            try
            {
                var data = await _googleSheetsService.GetVentasDataAsync();
                
                var meses = new[] { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio" };
                var coordinadoresData = new Dictionary<string, int[]>();
                
                // Obtener todos los coordinadores únicos
                var todosCoordinadores = data.Coordinadores.Values
                    .SelectMany(mesData => mesData.Keys)
                    .Distinct()
                    .ToList();

                foreach (var coord in todosCoordinadores)
                {
                    var ventasPorMes = meses.Select(mes => 
                        data.Coordinadores.GetValueOrDefault(mes, new Dictionary<string, int>())
                            .GetValueOrDefault(coord, 0)
                    ).ToArray();
                    
                    coordinadoresData[coord] = ventasPorMes;
                }

                var resultado = new
                {
                    Labels = new[] { "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul" },
                    Coordinadores = coordinadoresData
                };

                return Ok(resultado);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    error = "Error al obtener datos de coordinadores", 
                    message = ex.Message 
                });
            }
        }

        [HttpGet("productos")]
        public async Task<ActionResult<object>> GetProductos()
        {
            try
            {
                var data = await _googleSheetsService.GetVentasDataAsync();
                
                // Agregar productos de todos los meses
                var productosTotal = new Dictionary<string, int>();
                
                foreach (var mesData in data.Productos.Values)
                {
                    foreach (var (producto, cantidad) in mesData)
                    {
                        productosTotal[producto] = productosTotal.GetValueOrDefault(producto, 0) + cantidad;
                    }
                }

                var productosOrdenados = productosTotal
                    .OrderByDescending(p => p.Value)
                    .Take(8)
                    .ToList();

                var totalVentas = productosTotal.Values.Sum();

                var resultado = new
                {
                    Productos = productosOrdenados.Select(p => new
                    {
                        Nombre = p.Key,
                        Ventas = p.Value,
                        Porcentaje = Math.Round((double)p.Value / totalVentas * 100, 1)
                    }).ToArray()
                };

                return Ok(resultado);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    error = "Error al obtener datos de productos", 
                    message = ex.Message 
                });
            }
        }

        [HttpGet("ejecutivos/top/{cantidad:int?}")]
        public async Task<ActionResult<object>> GetTopEjecutivos(int cantidad = 15)
        {
            try
            {
                var data = await _googleSheetsService.GetVentasDataAsync();
                
                var meses = new[] { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio" };
                var ejecutivosData = new Dictionary<string, Dictionary<string, int>>();

                // Obtener todos los ejecutivos únicos
                var todosEjecutivos = data.Ejecutivos.Values
                    .SelectMany(mesData => mesData.Keys)
                    .Distinct()
                    .ToList();

                foreach (var ejecutivo in todosEjecutivos)
                {
                    var ventasPorMes = new Dictionary<string, int>();
                    var totalEjecutivo = 0;

                    foreach (var mes in meses)
                    {
                        var ventas = data.Ejecutivos.GetValueOrDefault(mes, new Dictionary<string, int>())
                            .GetValueOrDefault(ejecutivo, 0);
                        ventasPorMes[mes] = ventas;
                        totalEjecutivo += ventas;
                    }

                    ventasPorMes["total"] = totalEjecutivo;
                    ventasPorMes["promedio"] = totalEjecutivo / meses.Length;
                    ejecutivosData[ejecutivo] = ventasPorMes;
                }

                var topEjecutivos = ejecutivosData
                    .OrderByDescending(e => e.Value["total"])
                    .Take(cantidad)
                    .Select((e, index) => new
                    {
                        Posicion = index + 1,
                        Nombre = e.Key,
                        Coordinador = data.EjecutivoCoordinador.GetValueOrDefault(e.Key, "Sin Asignar"),
                        Enero = e.Value["enero"],
                        Febrero = e.Value["febrero"],
                        Marzo = e.Value["marzo"],
                        Abril = e.Value["abril"],
                        Mayo = e.Value["mayo"],
                        Junio = e.Value["junio"],
                        Julio = e.Value["julio"],
                        Total = e.Value["total"],
                        Promedio = Math.Round((double)e.Value["promedio"], 1)
                    })
                    .ToArray();

                return Ok(new { Ejecutivos = topEjecutivos });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    error = "Error al obtener top ejecutivos", 
                    message = ex.Message 
                });
            }
        }
    }
}