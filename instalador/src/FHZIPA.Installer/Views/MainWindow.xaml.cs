using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FHZIPA.Installer.Services;
using FHZIPA.Installer.ViewModels;

namespace FHZIPA.Installer.Views;

/// <summary>
/// A janela do instalador, com a barra de titulo desenhada pelo proprio
/// aplicativo.
/// </summary>
public partial class MainWindow : Window
{
    private const double MinimumFunctionalWidth = 760;
    private const double MinimumFunctionalHeight = 520;
    private const double MaximumWidth = 1080;
    private const double MaximumHeight = 700;
    private const double CompactBreakpoint = 820;

    private readonly ILogService? _log;

    public MainWindow()
    {
        InitializeComponent();
        Hook();
    }

    public MainWindow(ILogService log) : this()
    {
        _log = log;
    }

    private void Hook()
    {
        DataContextChanged += OnDataContextChanged;
        PreviewKeyDown += OnPreviewKeyDown;
        StateChanged += OnStateChanged;
        SizeChanged += OnSizeChanged;
        Loaded += OnLoaded;
    }

    /// <summary>
    /// A janela e compacta: um terco da area util do monitor, com um
    /// minimo que mantem o conteudo legivel.
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyInitialSize();
        LogGeometry();
    }

    private void ApplyInitialSize()
    {
        var workArea = SystemParameters.WorkArea;

        var targetWidth = workArea.Width / 3.0;
        var targetHeight = workArea.Height / 3.0;

        var width = Math.Clamp(targetWidth, MinimumFunctionalWidth, MaximumWidth);
        var height = Math.Clamp(targetHeight, MinimumFunctionalHeight, MaximumHeight);

        Width = width;
        Height = height;
        Left = workArea.Left + (workArea.Width - width) / 2;
        Top = workArea.Top + (workArea.Height - height) / 2;
    }

    private void LogGeometry()
    {
        if (_log is null)
        {
            return;
        }

        var workArea = SystemParameters.WorkArea;
        var dpi = VisualTreeHelper.GetDpi(this);

        _log.Info(
            "Janela: solicitado {0}x{1} (1/3 da area util {2}x{3}), aplicado {4}x{5} (minimo funcional {6}x{7}). DPI {8} ({9}%), monitor {10}x{11}.",
            (int)(workArea.Width / 3),
            (int)(workArea.Height / 3),
            workArea.Width,
            workArea.Height,
            (int)Width,
            (int)Height,
            (int)MinimumFunctionalWidth,
            (int)MinimumFunctionalHeight,
            dpi.PixelsPerInchX,
            (int)(dpi.DpiScaleX * 100),
            SystemParameters.PrimaryScreenWidth,
            SystemParameters.PrimaryScreenHeight);
    }

    /// <summary>
    /// O layout compacto reduz as margens decorativas e estreita o painel
    /// lateral, sem mexer nos tamanhos de fonte.
    /// </summary>
    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var compact = e.NewSize.Width < CompactBreakpoint;

        ViewHost.Margin = compact
            ? new Thickness(14, 12, 14, 8)
            : new Thickness(22, 18, 22, 12);

        if (LeftColumn is not null && RightColumn is not null)
        {
            // A proporcao do painel lateral. No layout compacto ele
            // estreita, sem mexer nos tamanhos de fonte.
            LeftColumn.Width = new GridLength(compact ? 0.85 : 1, GridUnitType.Star);
            RightColumn.Width = new GridLength(compact ? 2.0 : 1.9, GridUnitType.Star);
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is MainViewModel old)
        {
            old.RequestClose -= OnRequestClose;
        }

        if (e.NewValue is MainViewModel current)
        {
            current.RequestClose += OnRequestClose;
        }
    }

    private void OnRequestClose(object? sender, EventArgs e) => Close();

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (MaximizeButton is null)
        {
            return;
        }

        MaximizeButton.ToolTip = WindowState == WindowState.Maximized ? "Restaurar" : "Maximizar";
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// A barra de titulo arrasta a janela. O duplo clique maximiza.
    /// </summary>
    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            return;
        }

        if (WindowState == WindowState.Normal)
        {
            DragMove();
        }
    }

    /// <summary>
    /// O teclado funciona sem o mouse: Enter executa a acao principal e
    /// Escape cancela.
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (ViewModel?.CancelCommand.CanExecute(null) == true)
            {
                ViewModel.CancelCommand.Execute(null);
                e.Handled = true;
            }

            return;
        }

        if (e.Key != Key.Enter)
        {
            return;
        }

        if (e.OriginalSource is System.Windows.Controls.TextBox)
        {
            return;
        }

        if (ViewModel?.NextCommand.CanExecute(null) == true)
        {
            ViewModel.NextCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>
    /// O fechamento passa pelo mesmo cancelamento das operacoes em curso.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel is { IsBusy: true })
        {
            var answer = MessageBox.Show(
                "A instalação ainda está em andamento. Deseja cancelar e sair?",
                "FHZIPA INJECT",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (answer != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            viewModel.Cancel();
        }

        base.OnClosing(e);
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;
}
