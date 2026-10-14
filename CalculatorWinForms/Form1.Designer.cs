#nullable enable
namespace CalculatorWinForms;

partial class Form1
{
    private System.ComponentModel.IContainer? components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(380, 620);
        MinimumSize = new Size(320, 540);
        Text = "Scientific Calculator";
        StartPosition = FormStartPosition.CenterScreen;
    }
}
