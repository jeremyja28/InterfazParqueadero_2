using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using ClosedXML.Excel;

namespace InterfazParqueadero
{
    // -------------------------------------------------------------------------
    // Fila genérica de exportación — desacoplada del DataGridView concreto.
    // -------------------------------------------------------------------------
    public class FilaExport
    {
        public string FechaEntrada    { get; set; } = "";
        public string FechaSalida     { get; set; } = "";
        public string Estado          { get; set; } = "";
        public string TipoIngreso     { get; set; } = "";
        public string NombreCompleto  { get; set; } = "";
        public string Placa           { get; set; } = "";
        public string Duracion        { get; set; } = "";
        public string Id              { get; set; } = "";
    }

    // =========================================================================
    // ExportService — exporta a JSON y Excel real (.xlsx).
    //
    // Carpeta destino: <directorio_exe>\Exportaciones\
    // Abre la carpeta automáticamente después de guardar.
    // =========================================================================
    public static class ExportService
    {
        private static readonly string _baseDir =
            Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".";

        public static string CarpetaExportaciones =>
            Path.Combine(_baseDir, "Exportaciones");

        // -----------------------------------------------------------------
        // Extraer filas del DataGridView activo.
        // -----------------------------------------------------------------
        public static List<FilaExport> ExtraerFilas(DataGridView dgv)
        {
            var lista = new List<FilaExport>();
            foreach (DataGridViewRow row in dgv.Rows)
            {
                if (row.IsNewRow) continue;
                lista.Add(new FilaExport
                {
                    FechaEntrada   = row.Cells["FechaEntrada"]?.Value?.ToString()   ?? "",
                    FechaSalida    = row.Cells["FechaSalida"]?.Value?.ToString()    ?? "",
                    Estado         = row.Cells["Estado"]?.Value?.ToString()         ?? "",
                    TipoIngreso    = row.Cells["Tipo"]?.Value?.ToString()           ?? "",
                    NombreCompleto = row.Cells["NombreCompleto"]?.Value?.ToString() ?? "",
                    Placa          = row.Cells["Placa"]?.Value?.ToString()          ?? "",
                    Duracion       = row.Cells["Duracion"]?.Value?.ToString()       ?? "",
                    Id             = row.Cells["Id"]?.Value?.ToString()             ?? "",
                });
            }
            return lista;
        }

        // -----------------------------------------------------------------
        // Exportar a JSON.
        // -----------------------------------------------------------------
        public static string ExportarJSON(List<FilaExport> filas, string etiqueta = "historial")
        {
            AsegurarCarpeta();
            string nombre = $"{etiqueta}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
            string ruta   = Path.Combine(CarpetaExportaciones, nombre);

            string json = JsonSerializer.Serialize(filas,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ruta, json);
            return ruta;
        }

        // -----------------------------------------------------------------
        // Exportar a Excel (.xlsx) con formato de tabla.
        // -----------------------------------------------------------------
        public static string ExportarExcel(List<FilaExport> filas, string etiqueta = "historial")
        {
            AsegurarCarpeta();
            string nombre = $"{etiqueta}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.xlsx";
            string ruta   = Path.Combine(CarpetaExportaciones, nombre);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Historial");

            // ── Título ──────────────────────────────────────────────────────
            ws.Cell(1, 1).Value = "Historial de Accesos — Parqueadero";
            ws.Cell(1, 1).Style.Font.Bold     = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromArgb(10, 40, 116);
            ws.Range(1, 1, 1, 8).Merge();

            ws.Cell(2, 1).Value = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm:ss}  |  Registros: {filas.Count}";
            ws.Cell(2, 1).Style.Font.Italic   = true;
            ws.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;
            ws.Range(2, 1, 2, 8).Merge();

            // ── Encabezados ─────────────────────────────────────────────────
            string[] headers = { "Entrada", "Salida", "Estado", "Tipo", "Nombre", "Placa", "Duración", "ID" };
            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(4, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(10, 40, 116);
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            }

            // ── Datos ────────────────────────────────────────────────────────
            int fila = 5;
            bool par = false;
            foreach (var f in filas)
            {
                ws.Cell(fila, 1).Value = f.FechaEntrada;
                ws.Cell(fila, 2).Value = f.FechaSalida;
                ws.Cell(fila, 3).Value = f.Estado;
                ws.Cell(fila, 4).Value = f.TipoIngreso;
                ws.Cell(fila, 5).Value = f.NombreCompleto;
                ws.Cell(fila, 6).Value = f.Placa;
                ws.Cell(fila, 7).Value = f.Duracion;
                ws.Cell(fila, 8).Value = f.Id;

                // Color alternado + verde si ADENTRO
                XLColor fondo = f.Estado.Contains("ADENTRO")
                    ? XLColor.FromArgb(220, 248, 230)
                    : (par ? XLColor.FromArgb(240, 246, 255) : XLColor.White);

                var rango = ws.Range(fila, 1, fila, 8);
                rango.Style.Fill.BackgroundColor = fondo;
                rango.Style.Border.BottomBorder  = XLBorderStyleValues.Hair;

                if (f.Estado.Contains("ADENTRO"))
                    ws.Cell(fila, 3).Style.Font.FontColor = XLColor.FromArgb(30, 130, 60);

                fila++;
                par = !par;
            }

            // ── Bordes del bloque de datos ───────────────────────────────────
            if (filas.Count > 0)
            {
                var tabla = ws.Range(4, 1, fila - 1, 8);
                tabla.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                tabla.Style.Border.OutsideBorderColor = XLColor.FromArgb(81, 127, 164);
            }

            // ── Ajustar anchos ───────────────────────────────────────────────
            ws.Column(1).Width = 20;
            ws.Column(2).Width = 20;
            ws.Column(3).Width = 10;
            ws.Column(4).Width = 12;
            ws.Column(5).Width = 30;
            ws.Column(6).Width = 12;
            ws.Column(7).Width = 10;
            ws.Column(8).Width = 7;

            // Fila de encabezado fija (freeze)
            ws.SheetView.FreezeRows(4);

            wb.SaveAs(ruta);
            return ruta;
        }

        // -----------------------------------------------------------------
        // Abrir la carpeta de exportaciones en el Explorador de Windows.
        // -----------------------------------------------------------------
        public static void AbrirCarpeta()
        {
            AsegurarCarpeta();
            System.Diagnostics.Process.Start("explorer.exe", CarpetaExportaciones);
        }

        // -----------------------------------------------------------------
        private static void AsegurarCarpeta()
        {
            if (!Directory.Exists(CarpetaExportaciones))
                Directory.CreateDirectory(CarpetaExportaciones);
        }
    }
}
