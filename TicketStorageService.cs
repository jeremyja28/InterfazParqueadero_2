using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace InterfazParqueadero
{
    // ═══════════════════════════════════════════════════════════════
    // Servicio centralizado de persistencia JSON para tickets de visitantes.
    // La lista vive en TicketVisitanteForm.TicketsActivos (en memoria).
    // Este servicio sólo se ocupa de leer/escribir el archivo JSON.
    // ═══════════════════════════════════════════════════════════════
    public static class TicketStorageService
    {
        private static readonly string JsonPath =
            Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "tickets_visitantes.json");

        // DTO interno para serialización (coincide con el esquema ya existente)
        private sealed class TicketsDatos
        {
            public int Contador { get; set; } = 1000;
            public List<TicketVisitante> Tickets { get; set; } = new();
        }

        /// <summary>
        /// Persiste en disco la lista de tickets junto con el contador correlativo.
        /// Falla silenciosamente para no interrumpir el flujo del operador.
        /// </summary>
        public static void Guardar(List<TicketVisitante> tickets, int contador)
        {
            try
            {
                var datos = new TicketsDatos { Contador = contador, Tickets = tickets };
                var opciones = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(JsonPath, JsonSerializer.Serialize(datos, opciones));
            }
            catch { /* No bloquear si el sistema de archivos no está disponible */ }
        }

        /// <summary>
        /// Carga los tickets desde el archivo JSON.
        /// Devuelve una lista vacía y el contador inicial 1000 si el archivo no existe o está corrupto.
        /// </summary>
        public static (List<TicketVisitante> Tickets, int Contador) Cargar()
        {
            try
            {
                if (!File.Exists(JsonPath))
                    return (new List<TicketVisitante>(), 1000);

                var datos = JsonSerializer.Deserialize<TicketsDatos>(File.ReadAllText(JsonPath));
                if (datos == null)
                    return (new List<TicketVisitante>(), 1000);

                return (datos.Tickets ?? new List<TicketVisitante>(), datos.Contador);
            }
            catch
            {
                return (new List<TicketVisitante>(), 1000);
            }
        }
    }
}
