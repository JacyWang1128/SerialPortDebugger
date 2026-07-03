using System.Windows.Input;

namespace SerialPortDebugger.ViewModels.Base;

/// <summary>
/// 可绑定的 ICommand 实现，支持 Execute 和 CanExecute
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    /// <summary>
    /// 手动触发 CanExecuteChanged 重新查询
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}
