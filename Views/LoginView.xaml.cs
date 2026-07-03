using FirmasApp.Models;
using System.Windows;

namespace FirmasApp.Views;

public partial class LoginView : Window
{
    private readonly MainViewModel _viewModel;

    public LoginView(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }
}
