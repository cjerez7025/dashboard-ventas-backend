// Services/GoogleSheetsService.cs
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace TodoApi.Services
{
    public class GoogleSheetsService
    {
        private readonly SheetsService _sheetsService;
        private readonly string _spreadsheetId;

        public GoogleSheetsService(IConfiguration configuration)
        {
            // ID de tu Google Sheet (de la URL)
            _spreadsheetId = "1Mc1nJGsbXfQvGuGjNqwhLIO4cxk71BzaS_3-Fu-Mk58";

            // Configurar autenticación (usaremos API Key por simplicidad)
            var apiKey = configuration["GoogleSheets:ApiKey"];
            
            _sheetsService = new SheetsService(new BaseClientService.Initializer()
            {
                ApiKey = apiKey,
                ApplicationName = "Dashboard Ventas"
            });
        }

        public async Task<VentasData> GetVentasDataAsync()
        {
            var meses = new[] { "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio" };
            var ventasData = new VentasData
            {
                Totales = new Dictionary<string, VentasMes>(),
                Coordinadores = new Dictionary<string, Dictionary<string, int>>(),
                Ejecutivos = new Dictionary<string, Dictionary<string, int>>(),
                Modalidades = new Dictionary<string, Dictionary<string, int>>(),
                Productos = new Dictionary<string, Dictionary<string, int>>(),
                EjecutivoCoordinador = new Dictionary<string, string>()
            };

            foreach (var mes in meses)
            {
                try
                {
                    var mesData = await ProcessMesAsync(mes);
                    if (mesData != null)
                    {
                        var mesKey = mes.ToLower();
                        ventasData.Totales[mesKey] = mesData.Totales;
                        ventasData.Coordinadores[mesKey] = mesData.Coordinadores;
                        ventasData.Ejecutivos[mesKey] = mesData.Ejecutivos;
                        ventasData.Modalidades[mesKey] = mesData.Modalidades;
                        ventasData.Productos[mesKey] = mesData.Productos;
                        
                        // Combinar mapeo ejecutivo-coordinador
                        foreach (var mapping in mesData.EjecutivoCoordinador)
                        {
                            ventasData.EjecutivoCoordinador[mapping.Key] = mapping.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error procesando {mes}: {ex.Message}");
                }
            }

            return ventasData;
        }

        private async Task<MesDataResult?> ProcessMesAsync(string mes)
        {
            try
            {
                var range = $"{mes}!A:AC"; // Todas las columnas hasta AC
                var request = _sheetsService.Spreadsheets.Values.Get(_spreadsheetId, range);
                var response = await request.ExecuteAsync();

                if (response?.Values == null || response.Values.Count <= 1)
                {
                    return null;
                }

                var headers = response.Values[0].Select(h => h?.ToString() ?? "").ToList();
                var rows = response.Values.Skip(1).ToList();

                // Buscar índices de columnas
                var estadoIndex = FindColumnIndex(headers, "estado");
                var coordIndex = FindColumnIndex(headers, "nombre coordinador");
                var ejecutivoIndex = FindColumnIndex(headers, "nombre ejecutivo");
                var napIndex = 18; // Columna S (NAP)
                var modalidadIndex = 21; // Columna V
                var productoIndex = 23; // Columna X

                if (estadoIndex == -1)
                {
                    Console.WriteLine($"Columna Estado no encontrada en {mes}");
                    return null;
                }

                // Filtrar solo aprobados
                var aprobados = rows.Where(row => 
                    row.Count > estadoIndex && 
                    row[estadoIndex]?.ToString()?.Trim() == "Aprobada Servicing"
                ).ToList();

                Console.WriteLine($"{mes}: {aprobados.Count} aprobados de {rows.Count} total");

                // Procesar datos
                var coordDelMes = new Dictionary<string, int>();
                var ejecutivosDelMes = new Dictionary<string, int>();
                var modalidadesDelMes = new Dictionary<string, int>();
                var productosDelMes = new Dictionary<string, int>();
                var ejecutivoCoordinador = new Dictionary<string, string>();
                double napTotalMes = 0;

                foreach (var row in aprobados)
                {
                    // NAP real de columna S con parsing mejorado
                    if (napIndex < row.Count && !string.IsNullOrEmpty(row[napIndex]?.ToString()))
                    {
                        var napString = row[napIndex].ToString().Replace(",", ".");
                        if (double.TryParse(napString, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double nap))
                        {
                            napTotalMes += nap;
                        }
                        else
                        {
                            Console.WriteLine($"No se pudo parsear NAP: {row[napIndex]?.ToString()}");
                        }
                    }

                    // Coordinadores
                    if (coordIndex != -1 && coordIndex < row.Count && !string.IsNullOrEmpty(row[coordIndex]?.ToString()))
                    {
                        var coord = row[coordIndex].ToString()!.Trim();
                        coordDelMes[coord] = coordDelMes.GetValueOrDefault(coord, 0) + 1;
                    }

                    // Ejecutivos y mapeo con coordinador
                    if (ejecutivoIndex != -1 && ejecutivoIndex < row.Count && !string.IsNullOrEmpty(row[ejecutivoIndex]?.ToString()))
                    {
                        var ejecutivo = row[ejecutivoIndex].ToString()!.Trim();
                        ejecutivosDelMes[ejecutivo] = ejecutivosDelMes.GetValueOrDefault(ejecutivo, 0) + 1;

                        // Mapear ejecutivo con coordinador
                        if (coordIndex != -1 && coordIndex < row.Count && !string.IsNullOrEmpty(row[coordIndex]?.ToString()))
                        {
                            var coordinador = row[coordIndex].ToString()!.Trim();
                            ejecutivoCoordinador[ejecutivo] = coordinador;
                        }
                    }

                    // Modalidades
                    if (modalidadIndex < row.Count && !string.IsNullOrEmpty(row[modalidadIndex]?.ToString()))
                    {
                        var modalidad = row[modalidadIndex].ToString()!.Trim();
                        modalidadesDelMes[modalidad] = modalidadesDelMes.GetValueOrDefault(modalidad, 0) + 1;
                    }

                    // Productos
                    if (productoIndex < row.Count && !string.IsNullOrEmpty(row[productoIndex]?.ToString()))
                    {
                        var producto = row[productoIndex].ToString()!.Trim();
                        // Limpiar nombre del producto
                        producto = System.Text.RegularExpressions.Regex.Replace(producto, @"^PLAN\s+\d+:\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        productosDelMes[producto] = productosDelMes.GetValueOrDefault(producto, 0) + 1;
                    }
                }

                Console.WriteLine($"{mes}: NAP Total = {napTotalMes}");

                return new MesDataResult
                {
                    Totales = new VentasMes 
                    { 
                        Ventas = aprobados.Count, 
                        Nap = Math.Round(napTotalMes, 2) 
                    },
                    Coordinadores = coordDelMes,
                    Ejecutivos = ejecutivosDelMes,
                    Modalidades = modalidadesDelMes,
                    Productos = productosDelMes,
                    EjecutivoCoordinador = ejecutivoCoordinador
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error procesando {mes}: {ex.Message}");
                return null;
            }
        }

        private int FindColumnIndex(List<string> headers, string searchTerm)
        {
            return headers.FindIndex(h => 
                !string.IsNullOrEmpty(h) && 
                h.ToLower().Contains(searchTerm.ToLower())
            );
        }
    }

    // Clases de datos
    public class VentasData
    {
        public Dictionary<string, VentasMes> Totales { get; set; } = new();
        public Dictionary<string, Dictionary<string, int>> Coordinadores { get; set; } = new();
        public Dictionary<string, Dictionary<string, int>> Ejecutivos { get; set; } = new();
        public Dictionary<string, Dictionary<string, int>> Modalidades { get; set; } = new();
        public Dictionary<string, Dictionary<string, int>> Productos { get; set; } = new();
        public Dictionary<string, string> EjecutivoCoordinador { get; set; } = new();
    }

    public class VentasMes
    {
        public int Ventas { get; set; }
        public double Nap { get; set; }
    }

    public class MesDataResult
    {
        public VentasMes Totales { get; set; } = new();
        public Dictionary<string, int> Coordinadores { get; set; } = new();
        public Dictionary<string, int> Ejecutivos { get; set; } = new();
        public Dictionary<string, int> Modalidades { get; set; } = new();
        public Dictionary<string, int> Productos { get; set; } = new();
        public Dictionary<string, string> EjecutivoCoordinador { get; set; } = new();
    }
}