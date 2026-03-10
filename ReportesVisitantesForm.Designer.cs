namespace InterfazParqueadero
{
    partial class ReportesVisitantesForm
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
            
            // ReportesVisitantesForm
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(900, 650);
            Font = new Font("Segoe UI", 10F);
            Name = "ReportesVisitantesForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Reportes - Tickets de Visitantes";
            Load += ReportesVisitantesForm_Load;
            
            ResumeLayout(false);
        }

        #endregion
    }
}
