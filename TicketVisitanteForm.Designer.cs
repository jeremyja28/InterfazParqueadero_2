namespace InterfazParqueadero
{
    partial class TicketVisitanteForm
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
            
            // TicketVisitanteForm
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1000, 700);
            Font = new Font("Segoe UI", 10F);
            Name = "TicketVisitanteForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Tickets de Visitantes";
            
            ResumeLayout(false);
        }

        #endregion
    }
}
