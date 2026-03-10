using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace InterfazParqueadero
{
    // ═══════════════════════════════════════════════════════════════════════
    // MODELO — representa un usuario del sistema guardado en JSON
    // ═══════════════════════════════════════════════════════════════════════
    public class UsuarioSistema
    {
        /// <summary>Nombre de inicio de sesión (único, sin distinción de mayúsculas).</summary>
        public string Username      { get; set; } = "";

        /// <summary>Contraseña hasheada con SHA-256 (hex minúscula).</summary>
        public string PasswordHash  { get; set; } = "";

        /// <summary>Rol: "Operador" | "Administrador"  (SuperAdministrador es hardcoded).</summary>
        public string Rol           { get; set; } = "Operador";

        /// <summary>Nombre que se muestra en la interfaz, p. ej. "Fausto".</summary>
        public string NombreMostrar { get; set; } = "";

        /// <summary>Garita asignada, p. ej. "Garita Principal".</summary>
        public string Garita        { get; set; } = "Garita Principal";

        /// <summary>Fecha y hora en que fue creado este usuario.</summary>
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        /// <summary>Nombre del Super Administrador que creó este usuario.</summary>
        public string CreadoPor     { get; set; } = "Sistema";

        /// <summary>Indica si la cuenta está activa. Las cuentas inactivas no pueden iniciar sesión.</summary>
        public bool IsActivo        { get; set; } = true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SERVICIO — CRUD sobre usuarios_sistema.json
    // ═══════════════════════════════════════════════════════════════════════
    public static class UserService
    {
        // ── Ruta del archivo JSON ────────────────────────────────────────────
        public static string RutaArchivo =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "usuarios_sistema.json");

        private static readonly JsonSerializerOptions _opts = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        // ── Obtener todos los usuarios desde JSON ────────────────────────────
        public static List<UsuarioSistema> ObtenerTodos()
        {
            try
            {
                if (!File.Exists(RutaArchivo)) return new List<UsuarioSistema>();
                string json = File.ReadAllText(RutaArchivo, Encoding.UTF8);
                return JsonSerializer.Deserialize<List<UsuarioSistema>>(json, _opts)
                       ?? new List<UsuarioSistema>();
            }
            catch
            {
                return new List<UsuarioSistema>();
            }
        }

        private static void Guardar(List<UsuarioSistema> lista)
        {
            string json = JsonSerializer.Serialize(lista, _opts);
            File.WriteAllText(RutaArchivo, json, Encoding.UTF8);
        }

        // ── Agregar usuario ──────────────────────────────────────────────────
        /// <returns>true si fue creado; false si el username ya existe.</returns>
        public static bool AgregarUsuario(UsuarioSistema nuevo, string passwordPlano)
        {
            var lista = ObtenerTodos();
            if (lista.Any(u => u.Username.Equals(nuevo.Username, StringComparison.OrdinalIgnoreCase)))
                return false;

            nuevo.PasswordHash = HashPassword(passwordPlano);
            lista.Add(nuevo);
            Guardar(lista);
            return true;
        }

        // ── Activar / Inactivar usuario ──────────────────────────────────────
        /// <returns>El nuevo valor de IsActivo, o null si el usuario no existe.</returns>
        public static bool? ToggleActivo(string username)
        {
            var lista = ObtenerTodos();
            var u = lista.FirstOrDefault(x =>
                x.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (u == null) return null;
            u.IsActivo = !u.IsActivo;
            Guardar(lista);
            return u.IsActivo;
        }

        // ── Validar credenciales ─────────────────────────────────────────────
        /// <returns>El UsuarioSistema si las credenciales son válidas y la cuenta está activa; null si no.</returns>
        public static UsuarioSistema? ValidarCredenciales(string username, string passwordPlano)
        {
            string hash = HashPassword(passwordPlano);
            return ObtenerTodos().FirstOrDefault(u =>
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) &&
                u.PasswordHash == hash &&
                u.IsActivo);
        }

        // ── Buscar por username (sin validar contraseña) ─────────────────────
        /// <returns>El UsuarioSistema o null si no existe.</returns>
        public static UsuarioSistema? BuscarPorUsername(string username)
        {
            return ObtenerTodos().FirstOrDefault(u =>
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        }

        // ── Hash SHA-256 (hex minúscula) ─────────────────────────────────────
        public static string HashPassword(string password)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
