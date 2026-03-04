namespace InterfazParqueadero
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
          try
          {
            ApplicationConfiguration.Initialize();

            // ═══════════════════════════════════════════════════════
            // Flujo: LoginForm → Form1 (Dashboard)
            // Si el usuario cierra sesión, vuelve al login
            // ═══════════════════════════════════════════════════════
            bool continuarApp = true;

            while (continuarApp)
            {
                using (var loginForm = new LoginForm())
                {
                    if (loginForm.ShowDialog() != DialogResult.OK)
                    {
                        // El usuario cerró el login → salir de la app
                        return;
                    }

                    var mainForm = new Form1
                    {
                        RolUsuario = loginForm.RolSeleccionado,
                        NombreOperador = loginForm.NombreUsuario,
                        GaritaAsignada = loginForm.GaritaAsignada
                    };

                    Application.Run(mainForm);

                    // Si el form devuelve Retry, el usuario pidió cerrar sesión
                    if (mainForm.DialogResult != DialogResult.Retry)
                    {
                        continuarApp = false;
                    }
                }
            }
          }
          catch (Exception ex)
          {
            var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
            System.IO.File.WriteAllText(logPath, $"{DateTime.Now}\n{ex}\n");
            MessageBox.Show($"Error fatal:\n{ex.Message}\n\nDetalle guardado en crash.log", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
          }
        }
    }
}