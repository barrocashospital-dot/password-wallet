using System.Windows.Controls;

namespace Vault.App.Views;

public partial class VaultView : UserControl
{
    public VaultView()
    {
        InitializeComponent();
        Loaded += (_, _) => Focus();
    }
}
