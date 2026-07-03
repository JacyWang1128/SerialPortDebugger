namespace SerialPortDebugger.Models;

/// <summary>
/// 串口配置参数模型
/// </summary>
public class SerialPortConfig
{
    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public System.IO.Ports.Parity Parity { get; set; } = System.IO.Ports.Parity.None;
    public System.IO.Ports.StopBits StopBits { get; set; } = System.IO.Ports.StopBits.One;
    public System.IO.Ports.Handshake FlowControl { get; set; } = System.IO.Ports.Handshake.None;
}
