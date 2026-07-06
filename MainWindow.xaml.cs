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
            GlassFrameThickness = new Thickness(1000),
            UseAeroCaptionButtons = false,
            NonClientFrameEdges = NonClientFrameEdges.None
        };
        WindowChrome.SetWindowChrome(this, chrome);

        // 最大化时修正圆角
        UpdateMaximizeState();
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        UpdateMaximizeState();
    }

    Rect? originSize = null;

    private void UpdateMaximizeState()
    {
        if (IsMax)//WindowState == WindowState.Maximized)
        {
            originSize = new Rect() { X = this.Left, Y = this.Top, Width = this.Width, Height = this.Height };

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
            if (originSize.HasValue)
            {
                this.Height = originSize.Value.Height;
                this.Width = originSize.Value.Width;
                this.Top = originSize.Value.Top;
                this.Left = originSize.Value.Left;
            }
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
            IsMax = !IsMax;
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

    bool isMax = false;
    public bool IsMax
    {
        get { return isMax; }
        set
        {
            isMax = value;
            UpdateMaximizeState();
        }
    }

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
    {
        IsMax = !IsMax;
    }

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Normal)
            UpdateMaximizeState();
        else
            WindowState = WindowState.Normal;
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
