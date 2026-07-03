using System.Windows;

namespace FirmasApp.Views;

public partial class PasteUrlDialog : Window
{
    public string PastedText => TxtPaste.Text;

    public PasteUrlDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => TxtPaste.Focus();
    }

    private void BtnAceptar_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtPaste.Text))
        {
            MessageBox.Show(this, "Pegue una URL o código antes de continuar.",
                "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
        Close();
    }
}
