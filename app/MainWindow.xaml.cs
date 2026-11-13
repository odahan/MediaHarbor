using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MediaHarbor;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ((MainViewModel)DataContext).SearchRequested += () => QueryBox.Focus();
    }

    private void QualityKnobNext(object sender, MouseButtonEventArgs e) => RotateQualityKnob(1);

    private void QualityKnobPrevious(object sender, MouseButtonEventArgs e)
    {
        RotateQualityKnob(-1);
        e.Handled = true;
    }

    private void QualityKnobWheel(object sender, MouseWheelEventArgs e) => RotateQualityKnob(e.Delta > 0 ? -1 : 1);

    private void RotateQualityKnob(int step)
    {
        RadioButton[] options = [QualityBest, QualityNormal, QualityCustom];
        var current = Array.FindIndex(options, o => o.IsChecked == true);
        var next = current < 0 ? 0 : (current + step + options.Length) % options.Length;
        options[next].IsChecked = true;
    }

    private void DragTitle(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) Maximize(sender, e);
        else if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void Minimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseWindow(object sender, RoutedEventArgs e) => Close();
    private void OpenMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button) return;
        menu.DataContext = button.DataContext;
        menu.PlacementTarget = button;
        menu.IsOpen = true;
    }
}
