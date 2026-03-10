using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace InterfazParqueadero
{
    // -------------------------------------------------------------------------
    // Información de un usuario/vehículo con TAG registrado en UsoParqueadero.
    // -------------------------------------------------------------------------
    public class TagInfo
    {
        public int    UsoParqueaderoId { get; set; }
        public string Cedula           { get; set; } = "";
        public string NombreCompleto   { get; set; } = "";
        public string Placa            { get; set; } = "";
        public string Rol              { get; set; } = "";
        public string UnidadAcademica  { get; set; } = "";
        /// <summary>true = activo/pagado → autorizado a entrar. false = denegado.</summary>
        public bool   Activo           { get; set; }
    }

    // -------------------------------------------------------------------------
    // Caché local de TAGs sincronizada desde SQL DB.
    // Lookup O(1) sin latencia DB en el momento de acceso.
    // -------------------------------------------------------------------------
    public static class TagCacheService
    {
        // Reemplazo atómico de referencia → thread-safe para lecturas concurrentes.
        private static volatile Dictionary<string, TagInfo> _cache =
            new(StringComparer.OrdinalIgnoreCase);

        private static DateTime _ultimaSync = DateTime.MinValue;

        // -----------------------------------------------------------------
        // Busca un TAG en el caché. Retorna null si no está registrado.
        // -----------------------------------------------------------------
        public static TagInfo? BuscarTag(string? tagCode)
        {
            if (string.IsNullOrWhiteSpace(tagCode)) return null;
            _cache.TryGetValue(tagCode.Trim(), out var info);
            return info;
        }

        public static int      TotalTags           => _cache.Count;
        public static DateTime UltimaSincronizacion => _ultimaSync;

        // -----------------------------------------------------------------
        // Sincroniza el caché desde UsoParqueadero en SQL Server.
        // Falla en silencio para no bloquear la UI.
        // -----------------------------------------------------------------
        public static async Task SincronizarDesdeDB()
        {
            try
            {
                string connStr = DatabaseConfigService.BuildConnectionString();
                await using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                const string sql = @"
                    SELECT
                        u.UsoParqueaderoId,
                        TRY_CAST(u.Cedula AS BIGINT)                                           AS CedulaNum,
                        ISNULL(u.NombreInvitado, '')                                           AS Nombre,
                        ISNULL(u.Vehiculo1_Placa, ISNULL(u.Vehiculo2_Placa,
                               ISNULL(u.Moto1_Placa, '')))                                     AS Placa,
                        ISNULL(u.Activo, 0)                                                    AS Activo,
                        u.Tag,
                        u.Tag2,
                        ISNULL(r.Descripcion, '')                                              AS Rol,
                        ISNULL(u.UnidadAcademica, '')                                          AS Unidad
                    FROM UsoParqueadero u
                    LEFT JOIN RolesInstitucion r ON u.RolInstitucionId = r.RolInstitucionId
                    WHERE u.Tag IS NOT NULL OR u.Tag2 IS NOT NULL";

                await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 10 };
                await using var rdr = await cmd.ExecuteReaderAsync();

                var nuevo = new Dictionary<string, TagInfo>(StringComparer.OrdinalIgnoreCase);

                while (await rdr.ReadAsync())
                {
                    var info = new TagInfo
                    {
                        UsoParqueaderoId = rdr.GetInt32(rdr.GetOrdinal("UsoParqueaderoId")),
                        Cedula           = rdr["CedulaNum"] is DBNull ? ""
                                           : Convert.ToString(rdr["CedulaNum"]) ?? "",
                        NombreCompleto   = rdr["Nombre"]?.ToString()  ?? "",
                        Placa            = rdr["Placa"]?.ToString()   ?? "",
                        Activo           = Convert.ToBoolean(rdr["Activo"]),
                        Rol              = rdr["Rol"]?.ToString()     ?? "",
                        UnidadAcademica  = rdr["Unidad"]?.ToString()  ?? "",
                    };

                    string? tag1 = rdr["Tag"]?.ToString()?.Trim();
                    string? tag2 = rdr["Tag2"]?.ToString()?.Trim();

                    if (!string.IsNullOrEmpty(tag1)) nuevo[tag1] = info;
                    if (!string.IsNullOrEmpty(tag2)) nuevo[tag2] = info;
                }

                // Reemplazar referencia atómicamente
                _cache      = nuevo;
                _ultimaSync = DateTime.Now;
            }
            catch
            {
                // Falla en silencio — el caché anterior sigue activo
            }
        }

        // -----------------------------------------------------------------
        // Busca un TAG en caché. Si no está, consulta la BD directamente
        // para ese TAG específico y actualiza el caché. Nunca lanza.
        // -----------------------------------------------------------------
        public static async Task<TagInfo?> BuscarTagConFallbackDBAsync(string? tagCode)
        {
            if (string.IsNullOrWhiteSpace(tagCode)) return null;
            tagCode = tagCode.Trim();

            // Cache hit — ruta rápida
            if (_cache.TryGetValue(tagCode, out var cached))
                return cached;

            // Cache miss → consulta directa a BD para este tag puntual
            try
            {
                string connStr = DatabaseConfigService.BuildConnectionString();
                await using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                const string sql = @"
                    SELECT TOP 1
                        u.UsoParqueaderoId,
                        TRY_CAST(u.Cedula AS BIGINT)                            AS CedulaNum,
                        ISNULL(u.NombreInvitado, '')                            AS Nombre,
                        ISNULL(u.Vehiculo1_Placa, ISNULL(u.Vehiculo2_Placa,
                               ISNULL(u.Moto1_Placa, '')))                      AS Placa,
                        ISNULL(u.Activo, 0)                                     AS Activo,
                        u.Tag,
                        u.Tag2,
                        ISNULL(r.Descripcion, '')                               AS Rol,
                        ISNULL(u.UnidadAcademica, '')                           AS Unidad
                    FROM UsoParqueadero u
                    LEFT JOIN RolesInstitucion r ON u.RolInstitucionId = r.RolInstitucionId
                    WHERE u.Tag = @tag OR u.Tag2 = @tag";

                await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 5 };
                cmd.Parameters.AddWithValue("@tag", tagCode);
                await using var rdr = await cmd.ExecuteReaderAsync();

                if (!await rdr.ReadAsync()) return null;

                var info = new TagInfo
                {
                    UsoParqueaderoId = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0),
                    Cedula           = rdr.IsDBNull(1) ? "" : Convert.ToString(rdr.GetInt64(1)) ?? "",
                    NombreCompleto   = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                    Placa            = rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                    Activo           = !rdr.IsDBNull(4) && rdr.GetBoolean(4),
                    Rol              = rdr.IsDBNull(7) ? "" : rdr.GetString(7),
                    UnidadAcademica  = rdr.IsDBNull(8) ? "" : rdr.GetString(8),
                };

                // Insertar en caché para lecturas futuras (swap atómico)
                var nuevo = new Dictionary<string, TagInfo>(_cache, StringComparer.OrdinalIgnoreCase);
                if (!rdr.IsDBNull(5)) { var t  = rdr.GetString(5).Trim(); if (t  != "") nuevo[t]  = info; }
                if (!rdr.IsDBNull(6)) { var t2 = rdr.GetString(6).Trim(); if (t2 != "") nuevo[t2] = info; }
                _cache = nuevo;

                return info;
            }
            catch
            {
                return null; // BD no disponible — seguir sin dato
            }
        }
    }
}
