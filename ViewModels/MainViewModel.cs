using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SerialPortDebugger.Models;
using SerialPortDebugger.ViewModels.Base;

namespace SerialPortDebugger.ViewModels;

/// <summary>
/// 串口调试器主 ViewModel
/// </summary>
public class MainViewModel : ViewModelBase
{
    private SerialPort? _serialPort;
    private readonly System.Timers.Timer _autoSendTimer;
    private long _rxTotalBytes;
    private long _txTotalBytes;
    private readonly List<string> _presetCommands = new();

    #region 构造函数

    public MainViewModel()
    {
        _autoSendTimer = new System.Timers.Timer();
        _autoSendTimer.Elapsed += AutoSendTimer_Elapsed;
        _autoSendTimer.AutoReset = true;

        // 初始化命令
        OpenCloseCommand = new RelayCommand(OpenClose, () => !string.IsNullOrWhiteSpace(SelectedPortName));
        SendCommand = new RelayCommand(Send, () => IsPortOpen);
        RefreshPortsCommand = new RelayCommand(RefreshPorts);
        ClearReceiveCommand = new RelayCommand(ClearReceive);
        QuickSendCommand = new RelayCommand(QuickSend, () => IsPortOpen);
        AddPresetCommand = new RelayCommand(AddPreset);
        DeletePresetCommand = new RelayCommand(DeletePreset);

        // 初始化选项列表
        BaudRates = new ObservableCollection<string>
        {
            "1200", "2400", "4800", "9600", "14400", "19200",
            "38400", "56000", "57600", "115200", "128000", "256000", "460800", "921600"
        };
        DataBitsOptions = new ObservableCollection<string> { "8", "7", "6", "5" };
        ParityOptions = new ObservableCollection<string> { "None", "Odd", "Even", "Mark", "Space" };
        StopBitsOptions = new ObservableCollection<string> { "1", "1.5", "2" };
        FlowControlOptions = new ObservableCollection<string> { "None", "RTS/CTS", "XON/XOFF" };
        AvailablePorts = new ObservableCollection<string>();
        PresetCommands = new ObservableCollection<string>();

        // 默认值
        _selectedBaudRate = "9600";
        _selectedDataBits = "8";
        _selectedParity = "None";
        _selectedStopBits = "1";
        _selectedFlowControl = "None";
        _autoSendInterval = "1000";
        _isAutoScroll = true;
        _isSendNewLine = true;

        LoadPresets();
        RefreshPorts();
        UpdatePortState();
    }

    #endregion

    #region 串口列表属性

    public ObservableCollection<string> AvailablePorts { get; }

    private string? _selectedPortName;
    public string? SelectedPortName
    {
        get => _selectedPortName;
        set
        {
            if (SetProperty(ref _selectedPortName, value))
            {
                ((RelayCommand)OpenCloseCommand).RaiseCanExecuteChanged();
            }
        }
    }

    #endregion

    #region 串口参数属性

    public ObservableCollection<string> BaudRates { get; }

    private string _selectedBaudRate;
    public string SelectedBaudRate
    {
        get => _selectedBaudRate;
        set => SetProperty(ref _selectedBaudRate, value);
    }

    public ObservableCollection<string> DataBitsOptions { get; }

    private string _selectedDataBits;
    public string SelectedDataBits
    {
        get => _selectedDataBits;
        set => SetProperty(ref _selectedDataBits, value);
    }

    public ObservableCollection<string> ParityOptions { get; }

    private string _selectedParity;
    public string SelectedParity
    {
        get => _selectedParity;
        set => SetProperty(ref _selectedParity, value);
    }

    public ObservableCollection<string> StopBitsOptions { get; }

    private string _selectedStopBits;
    public string SelectedStopBits
    {
        get => _selectedStopBits;
        set => SetProperty(ref _selectedStopBits, value);
    }

    public ObservableCollection<string> FlowControlOptions { get; }

    private string _selectedFlowControl;
    public string SelectedFlowControl
    {
        get => _selectedFlowControl;
        set => SetProperty(ref _selectedFlowControl, value);
    }

    #endregion

    #region 端口状态属性

    private bool _isPortOpen;
    public bool IsPortOpen
    {
        get => _isPortOpen;
        private set
        {
            if (SetProperty(ref _isPortOpen, value))
            {
                OnPropertyChanged(nameof(OpenCloseButtonText));
                OnPropertyChanged(nameof(IsConfigEnabled));
                UpdatePortState();
                ((RelayCommand)SendCommand).RaiseCanExecuteChanged();
                ((RelayCommand)QuickSendCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string OpenCloseButtonText => IsPortOpen ? "关闭串口" : "打开串口";

    public bool IsConfigEnabled => !IsPortOpen;

    #endregion

    #region 接收相关属性

    private string _receivedText = string.Empty;
    public string ReceivedText
    {
        get => _receivedText;
        set => SetProperty(ref _receivedText, value);
    }

    private bool _isHexDisplay;
    public bool IsHexDisplay
    {
        get => _isHexDisplay;
        set => SetProperty(ref _isHexDisplay, value);
    }

    private bool _isAutoScroll;
    public bool IsAutoScroll
    {
        get => _isAutoScroll;
        set => SetProperty(ref _isAutoScroll, value);
    }

    private string _rxCountText = "接收: 0 字节";
    public string RxCountText
    {
        get => _rxCountText;
        set => SetProperty(ref _rxCountText, value);
    }

    #endregion

    #region 发送相关属性

    private string _sendText = string.Empty;
    public string SendText
    {
        get => _sendText;
        set => SetProperty(ref _sendText, value);
    }

    private bool _isHexSend;
    public bool IsHexSend
    {
        get => _isHexSend;
        set => SetProperty(ref _isHexSend, value);
    }

    private bool _isSendNewLine;
    public bool IsSendNewLine
    {
        get => _isSendNewLine;
        set => SetProperty(ref _isSendNewLine, value);
    }

    private string _autoSendInterval;
    public string AutoSendInterval
    {
        get => _autoSendInterval;
        set => SetProperty(ref _autoSendInterval, value);
    }

    private bool _isAutoSend;
    public bool IsAutoSend
    {
        get => _isAutoSend;
        set
        {
            if (SetProperty(ref _isAutoSend, value))
            {
                HandleAutoSendChanged();
            }
        }
    }

    private string _txCountText = "发送: 0 字节";
    public string TxCountText
    {
        get => _txCountText;
        set => SetProperty(ref _txCountText, value);
    }

    #endregion

    #region 状态栏属性

    private string _statusText = "就绪";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private string _portStateText = "未打开";
    public string PortStateText
    {
        get => _portStateText;
        set => SetProperty(ref _portStateText, value);
    }

    private Brush _portStateColor = new SolidColorBrush(Color.FromRgb(0xF4, 0x47, 0x5B));
    public Brush PortStateColor
    {
        get => _portStateColor;
        set => SetProperty(ref _portStateColor, value);
    }

    private string _logText = string.Empty;
    public string LogText
    {
        get => _logText;
        set => SetProperty(ref _logText, value);
    }

    /// <summary>
    /// 向运行日志追加一行（带时间戳）
    /// </summary>
    public void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        LogText += $"[{timestamp}] {message}\n";
    }

    #endregion

    #region 快捷发送属性

    public ObservableCollection<string> PresetCommands { get; }

    private string _quickSendText = string.Empty;
    public string QuickSendText
    {
        get => _quickSendText;
        set => SetProperty(ref _quickSendText, value);
    }

    #endregion

    #region 命令

    public ICommand OpenCloseCommand { get; }
    public ICommand SendCommand { get; }
    public ICommand RefreshPortsCommand { get; }
    public ICommand ClearReceiveCommand { get; }
    public ICommand QuickSendCommand { get; }
    public ICommand AddPresetCommand { get; }
    public ICommand DeletePresetCommand { get; }

    #endregion

    #region 命令实现

    private void OpenClose(object? _)
    {
        if (IsPortOpen)
            ClosePort();
        else
            OpenPort();
    }

    private void OpenPort()
    {
        try
        {
            var portName = SelectedPortName?.Trim();
            if (string.IsNullOrEmpty(portName))
            {
                MessageBox.Show("请选择串口号。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(SelectedBaudRate?.Trim(), out int baudRate) || baudRate <= 0)
            {
                MessageBox.Show("请输入有效的波特率。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var config = BuildConfig();
            _serialPort = new SerialPort(portName, baudRate, config.Parity, config.DataBits, config.StopBits)
            {
                ReadTimeout = 500,
                WriteTimeout = 500,
                ReadBufferSize = 16384,
                WriteBufferSize = 16384,
                Handshake = config.FlowControl
            };

            _serialPort.DataReceived += OnDataReceived;
            _serialPort.ErrorReceived += OnErrorReceived;
            _serialPort.Open();

            IsPortOpen = true;
            AppendReceivedText($"--- 串口 {portName} 已打开 [{baudRate},{config.DataBits},{config.Parity},{config.StopBits}] ---\n");
            SetStatus($"串口 {portName} 已打开", new SolidColorBrush(Color.FromRgb(0x4E, 0xCB, 0x71)));
        }
        catch (Exception ex)
        {
            _serialPort?.Dispose();
            _serialPort = null;
            MessageBox.Show($"打开串口失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus("打开失败", new SolidColorBrush(Color.FromRgb(0xF4, 0x47, 0x5B)));
        }
    }

    private void ClosePort()
    {
        try
        {
            StopAutoSend();

            if (_serialPort != null)
            {
                _serialPort.DataReceived -= OnDataReceived;
                _serialPort.ErrorReceived -= OnErrorReceived;

                if (_serialPort.IsOpen)
                    _serialPort.Close();

                _serialPort.Dispose();
                _serialPort = null;
            }

            IsPortOpen = false;
            AppendReceivedText("--- 串口已关闭 ---\n");
            SetStatus("串口已关闭", new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x90)));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"关闭串口失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Send(object? _)
    {
        if (_serialPort is not { IsOpen: true })
        {
            MessageBox.Show("请先打开串口。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var text = SendText ?? string.Empty;
        if (string.IsNullOrEmpty(text) && !IsSendNewLine)
            return;

        try
        {
            byte[] data = BuildSendData(text);

            _serialPort.Write(data, 0, data.Length);
            _txTotalBytes += data.Length;
            TxCountText = $"发送: {_txTotalBytes} 字节";

            // 回显
            AppendReceivedText(IsHexDisplay
                ? $"[TX] {BitConverter.ToString(data).Replace("-", " ")}\n"
                : $"[TX] {Encoding.ASCII.GetString(data)}\n");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"发送失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshPorts(object? _)
    {
        RefreshPorts();
    }

    private void ClearReceive(object? _)
    {
        ReceivedText = string.Empty;
        _rxTotalBytes = 0;
        RxCountText = "接收: 0 字节";
    }

    private void QuickSend(object? _)
    {
        if (_serialPort is not { IsOpen: true })
        {
            MessageBox.Show("请先打开串口。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var cmd = QuickSendText?.Trim();
        if (string.IsNullOrEmpty(cmd)) return;

        // 暂存并替换发送内容
        var previousText = SendText;
        var previousHex = IsHexSend;
        var previousNewLine = IsSendNewLine;

        SendText = cmd;
        IsHexSend = false;
        IsSendNewLine = true;

        Send(null);

        SendText = previousText;
        IsHexSend = previousHex;
        IsSendNewLine = previousNewLine;

        // 记录到预设
        if (!_presetCommands.Contains(cmd))
        {
            _presetCommands.Insert(0, cmd);
            RefreshPresetCollection();
            SavePresets();
        }
    }

    private void AddPreset(object? _)
    {
        var cmd = QuickSendText?.Trim();
        if (string.IsNullOrEmpty(cmd)) return;

        if (!_presetCommands.Contains(cmd))
        {
            _presetCommands.Add(cmd);
            RefreshPresetCollection();
            SavePresets();
            SetStatus($"已添加预设: {cmd}", new SolidColorBrush(Color.FromRgb(0x4E, 0xCB, 0x71)));
        }
        else
        {
            SetStatus("该预设已存在", new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x90)));
        }
    }

    private void DeletePreset(object? _)
    {
        var cmd = QuickSendText?.Trim();
        if (string.IsNullOrEmpty(cmd)) return;

        if (_presetCommands.Contains(cmd))
        {
            _presetCommands.Remove(cmd);
            RefreshPresetCollection();
            SavePresets();
            SetStatus($"已删除预设: {cmd}", new SolidColorBrush(Color.FromRgb(0x4E, 0xCB, 0x71)));
        }
    }

    #endregion

    #region 串口事件处理

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_serialPort is not { IsOpen: true }) return;

        try
        {
            int bytesToRead = _serialPort.BytesToRead;
            if (bytesToRead <= 0) return;

            byte[] buffer = new byte[bytesToRead];
            _serialPort.Read(buffer, 0, bytesToRead);

            Application.Current.Dispatcher.InvokeAsync(() => ProcessReceivedData(buffer));
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                AppendReceivedText($"--- 读取错误: {ex.Message} ---\n");
                Log($"读取错误: {ex.Message}");
            });
        }
    }

    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            AppendReceivedText($"--- 串口错误: {e.EventType} ---\n");
            Log($"串口错误: {e.EventType}");
        });
    }

    private void ProcessReceivedData(byte[] buffer)
    {
        _rxTotalBytes += buffer.Length;
        RxCountText = $"接收: {_rxTotalBytes} 字节";

        string text = IsHexDisplay
            ? BitConverter.ToString(buffer).Replace("-", " ") + "\n"
            : Encoding.ASCII.GetString(buffer);

        AppendReceivedText(text);
    }

    #endregion

    #region 自动发送

    private void HandleAutoSendChanged()
    {
        if (IsAutoSend)
            StartAutoSend();
        else
            StopAutoSend();
    }

    private void StartAutoSend()
    {
        if (!int.TryParse(AutoSendInterval?.Trim(), out int interval) || interval < 10)
        {
            MessageBox.Show("请输入有效的定时发送间隔（最小10ms）。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            IsAutoSend = false;
            return;
        }

        _autoSendTimer.Interval = interval;
        _autoSendTimer.Start();
        SetStatus($"定时发送已启动 (间隔: {interval}ms)", new SolidColorBrush(Color.FromRgb(0xF0, 0xA0, 0x40)));
    }

    private void StopAutoSend()
    {
        _autoSendTimer.Stop();
        if (IsPortOpen)
        {
            SetStatus("定时发送已停止", new SolidColorBrush(Color.FromRgb(0x4E, 0xCB, 0x71)));
        }
    }

    private void AutoSendTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(SendText))
        {
            Application.Current.Dispatcher.InvokeAsync(() => Send(null));
        }
    }

    #endregion

    #region 辅助方法

    private SerialPortConfig BuildConfig()
    {
        return new SerialPortConfig
        {
            PortName = SelectedPortName ?? "COM1",
            BaudRate = int.TryParse(SelectedBaudRate, out int b) ? b : 9600,
            DataBits = int.TryParse(SelectedDataBits, out int d) ? d : 8,
            Parity = SelectedParity switch
            {
                "Odd" => Parity.Odd,
                "Even" => Parity.Even,
                "Mark" => Parity.Mark,
                "Space" => Parity.Space,
                _ => Parity.None
            },
            StopBits = SelectedStopBits switch
            {
                "1.5" => StopBits.OnePointFive,
                "2" => StopBits.Two,
                _ => StopBits.One
            },
            FlowControl = SelectedFlowControl switch
            {
                "RTS/CTS" => Handshake.RequestToSend,
                "XON/XOFF" => Handshake.XOnXOff,
                _ => Handshake.None
            }
        };
    }

    private byte[] BuildSendData(string text)
    {
        byte[] data;

        if (IsHexSend)
        {
            string hexStr = text.Replace(" ", "").Replace("\r", "").Replace("\n", "").Replace("-", "");
            if (hexStr.Length % 2 != 0)
            {
                MessageBox.Show("HEX字符串长度必须为偶数。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return Array.Empty<byte>();
            }
            data = new byte[hexStr.Length / 2];
            for (int i = 0; i < hexStr.Length; i += 2)
            {
                data[i / 2] = Convert.ToByte(hexStr.Substring(i, 2), 16);
            }
        }
        else
        {
            data = Encoding.ASCII.GetBytes(text);
        }

        if (IsSendNewLine)
        {
            var newLine = Encoding.ASCII.GetBytes("\r\n");
            Array.Resize(ref data, data.Length + newLine.Length);
            Array.Copy(newLine, 0, data, data.Length - newLine.Length, newLine.Length);
        }

        return data;
    }

    private void RefreshPorts()
    {
        var previous = SelectedPortName;
        AvailablePorts.Clear();

        foreach (string port in SerialPort.GetPortNames())
        {
            AvailablePorts.Add(port);
        }

        if (AvailablePorts.Count > 0)
        {
            SelectedPortName = previous != null && AvailablePorts.Contains(previous)
                ? previous
                : AvailablePorts[0];
        }
    }

    private void AppendReceivedText(string text)
    {
        ReceivedText += text;
    }

    private void SetStatus(string message, Brush color)
    {
        StatusText = message;
        PortStateColor = color;
        Log(message);
    }

    private void UpdatePortState()
    {
        if (IsPortOpen)
        {
            PortStateText = $"已连接 {SelectedPortName}";
            PortStateColor = new SolidColorBrush(Color.FromRgb(0x4E, 0xCB, 0x71));
        }
        else
        {
            PortStateText = "未打开";
            PortStateColor = new SolidColorBrush(Color.FromRgb(0xF4, 0x47, 0x5B));
        }
    }

    private void RefreshPresetCollection()
    {
        PresetCommands.Clear();
        foreach (var cmd in _presetCommands)
        {
            PresetCommands.Add(cmd);
        }
    }

    #endregion

    #region 预设持久化

    private void LoadPresets()
    {
        try
        {
            string filePath = GetPresetFilePath();
            if (File.Exists(filePath))
            {
                var lines = File.ReadAllLines(filePath);
                _presetCommands.Clear();
                _presetCommands.AddRange(lines.Where(l => !string.IsNullOrWhiteSpace(l)));
                RefreshPresetCollection();
            }
        }
        catch { /* 忽略 */ }
    }

    private void SavePresets()
    {
        try
        {
            string filePath = GetPresetFilePath();
            string? dir = Path.GetDirectoryName(filePath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllLines(filePath, _presetCommands);
        }
        catch { /* 忽略 */ }
    }

    private static string GetPresetFilePath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "SerialPortDebugger", "presets.txt");
    }

    #endregion

    #region 清理

    /// <summary>
    /// 应用退出时清理资源
    /// </summary>
    public void Cleanup()
    {
        StopAutoSend();
        _autoSendTimer.Dispose();

        if (_serialPort != null)
        {
            _serialPort.DataReceived -= OnDataReceived;
            _serialPort.ErrorReceived -= OnErrorReceived;
            if (_serialPort.IsOpen)
                _serialPort.Close();
            _serialPort.Dispose();
            _serialPort = null;
        }
    }

    #endregion
}
