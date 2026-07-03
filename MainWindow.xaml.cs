using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shell;
using SerialPortDebugger.ViewModels;

namespace SerialPortDebugger;

/// <summary>
/// MainWindow - View 层，处理自定义窗口 chrome 和自动滚动
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        // 自动滚动接收区 + 日志区
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.ReceivedText) && _viewModel.IsAutoScroll)
            {
                txtReceive.ScrollToEnd();
            }
            if (e.PropertyName == nameof(MainViewModel.LogText))
            {
                txtLog.ScrollToEnd();
            }
        };
    }

    #region 自定义窗口 Chrome

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 设置 WindowChrome 以保留原生窗口行为（拖拽到边缘吸附、调整大小等）
        var chrome = new WindowChrome
        {
            CaptionHeight = 0,              // 自定义标题栏，不需要系统标题
            ResizeBorderThickness = new Thickness(6),
            CornerRadius = new CornerRadius(8),
            GlassFrameThickness = new Thickness(0),
            UseAeroCaptionButtons = false
        };
        WindowChrome.SetWindowChrome(this, chrome);

        // 最大化时修正圆角
        UpdateMaximizeState();
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        UpdateMaximizeState();
    }

    private void UpdateMaximizeState()
    {
        if (WindowState == WindowState.Maximized)
        {
            // 限制窗口不覆盖任务栏
            var wa = SystemParameters.WorkArea;
            Left = wa.Left;
            Top = wa.Top;
            Width = wa.Width;
            Height = wa.Height;

            WindowBorder.CornerRadius = new CornerRadius(0);
            WindowBorder.BorderThickness = new Thickness(0);
            TitleBar.CornerRadius = new CornerRadius(0);
            BtnMaximize.Content = "❐";
            BtnMaximize.ToolTip = "还原";
        }
        else
        {
            WindowBorder.CornerRadius = new CornerRadius(8);
            WindowBorder.BorderThickness = new Thickness(1);
            TitleBar.CornerRadius = new CornerRadius(8, 8, 0, 0);
            BtnMaximize.Content = "□";
            BtnMaximize.ToolTip = "最大化";
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // 双击标题栏切换最大化/还原
            ToggleMaximize();
        }
        else if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Cleanup();
        base.OnClosed(e);
    }
}
