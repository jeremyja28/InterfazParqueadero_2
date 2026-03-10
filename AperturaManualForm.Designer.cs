namespace InterfazParqueadero
{
    partial class AperturaManualForm
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
            
            // AperturaManualForm
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(480, 380);
            Font = new Font("Segoe UI", 10F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "AperturaManualForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Apertura Manual de Barrera";
            Load += AperturaManualForm_Load;
            
            ResumeLayout(false);
        }

        #endregion
    }
}
