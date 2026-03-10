namespace InterfazParqueadero
{
    partial class ParkingSlotForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            SuspendLayout();
            
            // ParkingSlotForm
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1100, 800);
            Font = new Font("Segoe UI", 10F);
            Name = "ParkingSlotForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Mapa de Parqueadero";
            WindowState = FormWindowState.Maximized;
            
            ResumeLayout(false);
        }

        #endregion
    }
}
