using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace InterfazParqueadero
{
    // ═══════════════════════════════════════════════════════════════════════════
    // RawPrinterHelper
    // Envía un arreglo de bytes directamente a la cola de impresión de Windows
    // usando la API nativa de winspool.drv (sin driver GDI, sin renderizado).
    // Esto es necesario para mandar comandos ESC/POS crudos a impresoras térmicas.
    // ═══════════════════════════════════════════════════════════════════════════
    internal static class RawPrinterHelper
    {
        // ── Estructuras requeridas por la API Win32 ──────────────────────────

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)] public string pDocName;
            [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
            [MarshalAs(UnmanagedType.LPStr)] public string pDataType;
        }

        // ── Importaciones de winspool.drv ────────────────────────────────────

        [DllImport("winspool.drv", EntryPoint = "OpenPrinterA",
                   SetLastError = true, CharSet = CharSet.Ansi,
                   ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool OpenPrinter(
            [MarshalAs(UnmanagedType.LPStr)] string szPrinter,
            out IntPtr hPrinter,
            IntPtr pd);

        [DllImport("winspool.drv", EntryPoint = "ClosePrinter",
                   SetLastError = true, ExactSpelling = true,
                   CallingConvention = CallingConvention.StdCall)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "StartDocPrinterA",
                   SetLastError = true, CharSet = CharSet.Ansi,
                   ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool StartDocPrinter(
            IntPtr hPrinter, int level,
            ref DOCINFOA di);

        [DllImport("winspool.drv", EntryPoint = "EndDocPrinter",
                   SetLastError = true, ExactSpelling = true,
                   CallingConvention = CallingConvention.StdCall)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "StartPagePrinter",
                   SetLastError = true, ExactSpelling = true,
                   CallingConvention = CallingConvention.StdCall)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "EndPagePrinter",
                   SetLastError = true, ExactSpelling = true,
                   CallingConvention = CallingConvention.StdCall)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "WritePrinter",
                   SetLastError = true, ExactSpelling = true,
                   CallingConvention = CallingConvention.StdCall)]
        private static extern bool WritePrinter(
            IntPtr hPrinter, IntPtr pBytes,
            int dwCount, out int dwWritten);

        // ── Método público ────────────────────────────────────────────────────

        /// <summary>
        /// Envía el arreglo de bytes <paramref name="bytes"/> directamente a la
        /// impresora identificada por <paramref name="printerName"/>.
        /// Lanza una <see cref="InvalidOperationException"/> si no puede abrir
        /// la impresora, para que el llamador pueda mostrar el mensaje de error.
        /// </summary>
        /// <param name="printerName">Nombre exacto de la impresora en Windows (ej. "M267D").</param>
        /// <param name="bytes">Secuencia ESC/POS lista para enviar.</param>
        public static void SendBytesToPrinter(string printerName, byte[] bytes)
        {
            if (string.IsNullOrWhiteSpace(printerName))
                throw new ArgumentException("El nombre de la impresora no puede estar vacío.", nameof(printerName));

            if (bytes == null || bytes.Length == 0)
                throw new ArgumentException("No hay datos para enviar a la impresora.", nameof(bytes));

            IntPtr hPrinter = IntPtr.Zero;
            IntPtr pUnmanagedBytes = IntPtr.Zero;

            try
            {
                // Abrir handle a la impresora
                if (!OpenPrinter(printerName, out hPrinter, IntPtr.Zero))
                    throw new InvalidOperationException(
                        $"No se pudo abrir la impresora \"{printerName}\".\n" +
                        $"Verifique que esté instalada y encendida.\n" +
                        $"Código de error Win32: {Marshal.GetLastWin32Error()}");

                // Configurar el documento de impresión en modo RAW
                var docInfo = new DOCINFOA
                {
                    pDocName  = "Ticket Visitante — PUCESA",
                    pOutputFile = null,
                    pDataType = "RAW"          // Evita cualquier procesamiento del spooler
                };

                if (!StartDocPrinter(hPrinter, 1, ref docInfo))
                    throw new InvalidOperationException(
                        $"StartDocPrinter falló (error {Marshal.GetLastWin32Error()}).");

                if (!StartPagePrinter(hPrinter))
                    throw new InvalidOperationException(
                        $"StartPagePrinter falló (error {Marshal.GetLastWin32Error()}).");

                // Copiar bytes administrados a memoria no administrada y escribir
                pUnmanagedBytes = Marshal.AllocCoTaskMem(bytes.Length);
                Marshal.Copy(bytes, 0, pUnmanagedBytes, bytes.Length);

                if (!WritePrinter(hPrinter, pUnmanagedBytes, bytes.Length, out int written))
                    throw new InvalidOperationException(
                        $"WritePrinter falló (error {Marshal.GetLastWin32Error()}). " +
                        $"Bytes escritos: {written}/{bytes.Length}");
            }
            finally
            {
                // Liberar recursos independientemente del resultado
                if (pUnmanagedBytes != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(pUnmanagedBytes);

                if (hPrinter != IntPtr.Zero)
                {
                    EndPagePrinter(hPrinter);
                    EndDocPrinter(hPrinter);
                    ClosePrinter(hPrinter);
                }
            }
        }
    }


    // ═══════════════════════════════════════════════════════════════════════════
    // EscPosBuilder
    // Constructor fluido de comandos ESC/POS para impresoras térmicas.
    // Arma un List<byte> con los comandos y permite obtener el byte[] final.
    //
    // Uso típico:
    //   byte[] ticket = new EscPosBuilder()
    //       .Init()
    //       .Center()
    //       .Bold(true).Text("PUCESA").NewLine()
    //       .Bold(false).Left()
    //       .Text("Placa: ABC-1234").NewLine()
    //       .Feed(3)
    //       .FullCut()
    //       .Build();
    // ═══════════════════════════════════════════════════════════════════════════
    internal sealed class EscPosBuilder
    {
        // ISO-8859-1 (Latin1) — siempre disponible en .NET 5+ sin registro adicional.
        // Cubre todos los caracteres en español y es compatible con impresoras ESC/POS.
        private static readonly Encoding PrintEncoding = Encoding.Latin1;

        private readonly List<byte> _buffer = new();

        // ── Comandos ESC/POS (constantes hex) ────────────────────────────────

        private static readonly byte[] CMD_INIT       = { 0x1B, 0x40 };           // ESC @  — Inicializar
        private static readonly byte[] CMD_ALIGN_LEFT = { 0x1B, 0x61, 0x00 };     // ESC a 0 — Alinear izquierda
        private static readonly byte[] CMD_ALIGN_CENTER = { 0x1B, 0x61, 0x01 };   // ESC a 1 — Alinear centro
        private static readonly byte[] CMD_ALIGN_RIGHT  = { 0x1B, 0x61, 0x02 };   // ESC a 2 — Alinear derecha
        private static readonly byte[] CMD_BOLD_ON    = { 0x1B, 0x45, 0x01 };     // ESC E 1 — Negrita activar
        private static readonly byte[] CMD_BOLD_OFF   = { 0x1B, 0x45, 0x00 };     // ESC E 0 — Negrita desactivar
        private static readonly byte[] CMD_DOUBLE_HEIGHT_ON  = { 0x1B, 0x21, 0x10 }; // ESC ! 16 — Doble alto
        private static readonly byte[] CMD_DOUBLE_HEIGHT_OFF = { 0x1B, 0x21, 0x00 }; // ESC !  0 — Normal
        private static readonly byte   CMD_LF         = 0x0A;                     // LF     — Salto de línea
        private static readonly byte[] CMD_FULL_CUT   = { 0x1D, 0x56, 0x00 };     // GS V 0 — Corte total
        private static readonly byte[] CMD_PARTIAL_CUT = { 0x1D, 0x56, 0x01 };   // GS V 1 — Corte parcial

        // Barcode CODE128: GS k 73 <n> <data>
        // GS k — 0x1D 0x6B
        // Tipo CODE128 en modo extendido = 0x49 (73 decimal)
        private const byte BARCODE_TYPE_CODE128 = 0x49;
        private static readonly byte[] CMD_BARCODE_HRI_BELOW = { 0x1D, 0x48, 0x02 }; // GS H 2 — texto bajo barras
        private static readonly byte[] CMD_BARCODE_HEIGHT    = { 0x1D, 0x68, 0x50 }; // GS h 80 — alto 80 puntos
        private static readonly byte[] CMD_BARCODE_WIDTH     = { 0x1D, 0x77, 0x02 }; // GS w 2 — ancho módulo 2

        // ── API fluida ────────────────────────────────────────────────────────

        /// <summary>Inicializa la impresora (ESC @). Siempre debe ser el primer comando.</summary>
        public EscPosBuilder Init()
        {
            _buffer.AddRange(CMD_INIT);
            return this;
        }

        /// <summary>Alinea el texto a la izquierda.</summary>
        public EscPosBuilder Left()
        {
            _buffer.AddRange(CMD_ALIGN_LEFT);
            return this;
        }

        /// <summary>Alinea el texto al centro.</summary>
        public EscPosBuilder Center()
        {
            _buffer.AddRange(CMD_ALIGN_CENTER);
            return this;
        }

        /// <summary>Alinea el texto a la derecha.</summary>
        public EscPosBuilder Right()
        {
            _buffer.AddRange(CMD_ALIGN_RIGHT);
            return this;
        }

        /// <summary>Activa o desactiva la negrita.</summary>
        public EscPosBuilder Bold(bool on)
        {
            _buffer.AddRange(on ? CMD_BOLD_ON : CMD_BOLD_OFF);
            return this;
        }

        /// <summary>Activa o desactiva el doble alto (útil para títulos).</summary>
        public EscPosBuilder DoubleHeight(bool on)
        {
            _buffer.AddRange(on ? CMD_DOUBLE_HEIGHT_ON : CMD_DOUBLE_HEIGHT_OFF);
            return this;
        }

        /// <summary>
        /// Añade texto plano codificado en CP850.
        /// No inserta salto de línea automático; use <see cref="NewLine"/> o <see cref="TextLine"/>.
        /// </summary>
        public EscPosBuilder Text(string text)
        {
            if (!string.IsNullOrEmpty(text))
                _buffer.AddRange(PrintEncoding.GetBytes(text));
            return this;
        }

        /// <summary>Inserta un salto de línea (LF = 0x0A).</summary>
        public EscPosBuilder NewLine()
        {
            _buffer.Add(CMD_LF);
            return this;
        }

        /// <summary>Atajo: añade texto y un salto de línea al final.</summary>
        public EscPosBuilder TextLine(string text)
            => Text(text).NewLine();

        /// <summary>
        /// Avanza <paramref name="lines"/> líneas en blanco para separar secciones
        /// o crear espacio antes del corte.
        /// </summary>
        public EscPosBuilder Feed(int lines = 1)
        {
            for (int i = 0; i < lines; i++)
                _buffer.Add(CMD_LF);
            return this;
        }

        /// <summary>
        /// Imprime una línea separadora de guiones del ancho indicado (default 32 chars = 58 mm).
        /// </summary>
        public EscPosBuilder Separator(int width = 32, char ch = '-')
        {
            _buffer.AddRange(PrintEncoding.GetBytes(new string(ch, width)));
            _buffer.Add(CMD_LF);
            return this;
        }

        /// <summary>
        /// Imprime un código de barras CODE128 con el texto <paramref name="code"/>.
        /// El texto se imprime debajo de las barras automáticamente.
        /// Si el código o la impresora no soportan CODE128, la secuencia se ignora silenciosamente.
        /// </summary>
        public EscPosBuilder Barcode128(string code)
        {
            if (string.IsNullOrEmpty(code)) return this;

            byte[] codeBytes = Encoding.ASCII.GetBytes(code);
            if (codeBytes.Length == 0 || codeBytes.Length > 255) return this;

            // Configuración previa
            _buffer.AddRange(CMD_BARCODE_HRI_BELOW);  // texto debajo
            _buffer.AddRange(CMD_BARCODE_HEIGHT);      // alto 80 puntos
            _buffer.AddRange(CMD_BARCODE_WIDTH);       // ancho módulo 2

            // La Epson TM-T20III requiere que los datos CODE128 comiencen con '{B'
            // (0x7B 0x42) para seleccionar el subset B (ASCII 32-127).
            // La longitud n debe incluir estos 2 bytes de prefijo.
            byte[] prefix = new byte[] { 0x7B, 0x42 }; // {B
            int totalLen = prefix.Length + codeBytes.Length;
            if (totalLen > 255) return this;

            // GS k 73 <n_bytes> <{B> <data...>
            _buffer.Add(0x1D);                         // GS
            _buffer.Add(0x6B);                         // k
            _buffer.Add(BARCODE_TYPE_CODE128);          // 73 = CODE128
            _buffer.Add((byte)totalLen);               // longitud total ({B + datos)
            _buffer.AddRange(prefix);                  // {B — selector subset B
            _buffer.AddRange(codeBytes);               // datos ASCII del código
            _buffer.Add(CMD_LF);
            return this;
        }

        /// <summary>Realiza un corte total del papel (GS V 0).</summary>
        public EscPosBuilder FullCut()
        {
            _buffer.AddRange(CMD_FULL_CUT);
            return this;
        }

        /// <summary>Realiza un corte parcial del papel (GS V 1).</summary>
        public EscPosBuilder PartialCut()
        {
            _buffer.AddRange(CMD_PARTIAL_CUT);
            return this;
        }

        /// <summary>
        /// Devuelve el arreglo de bytes ESC/POS listo para enviar a
        /// <see cref="RawPrinterHelper.SendBytesToPrinter"/>.
        /// </summary>
        public byte[] Build() => _buffer.ToArray();
    }
}
