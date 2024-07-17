using Avalonia.Controls;
using Avalonia.Input;
using Serilog;
using WaifuGallery.ViewModels;
using WaifuGallery.Views;

namespace WaifuGallery;

public partial class MainWindow : Window
{
    private MainViewViewModel? MainViewViewModel => (Content as MainView)?.DataContext as MainViewViewModel;

    public MainWindow()
    {
        Content = new MainView()
        {
            DataContext = new MainViewViewModel()
        };
        KeyDown += OnKeyDown;
        InitializeComponent();
    }

    /// <summary>
    /// Send all keyboard events from the MainWindow to the MainViewModel.
    /// </summary>
    /// <param name="sender">MainWindow</param>
    /// <param name="e">KeyEventArgs</param>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        Log.Debug(e.KeyModifiers is KeyModifiers.None
            ? $"{e.Key} MainWindow"
            : $"{e.Key} {e.KeyModifiers} MainWindow");
        MainViewViewModel?.HandleKeyBoardEvent(e);
    }
}