namespace InterfazParqueadero
{
    partial class UserManagementForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            // La UI se construye completamente desde ConstruirUI() en UserManagementForm.cs
            this.Load += new System.EventHandler(this.UserManagementForm_Load);
            this.ResumeLayout(false);
        }
    }
}
