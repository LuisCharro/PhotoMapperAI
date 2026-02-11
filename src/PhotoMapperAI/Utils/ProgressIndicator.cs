using System.Text;

namespace PhotoMapperAI.Utils;

/// <summary>
/// Simple progress indicator for console output.
/// </summary>
public class ProgressIndicator
{
    private readonly string _taskName;
    private int _total;
    private int _current;
    private int _lastPercent;
    private readonly bool _useBar;
    private readonly int _barWidth;

    /// <summary>
    /// Creates a new progress indicator.
    /// </summary>
    /// <param name="taskName">Name of the task being tracked</param>
    /// <param name="total">Total number of items to process</param>
    /// <param name="useBar">Whether to show a progress bar</param>
    public ProgressIndicator(string taskName, int total, bool useBar = true)
    {
        _taskName = taskName;
        _total = total;
        _current = 0;
        _lastPercent = -1;
        _useBar = useBar;
        _barWidth = 30;
    }

    /// <summary>
    /// Updates progress by one item.
    /// </summary>
    /// <param name="itemName">Optional name of the current item being processed</param>
    public void Update(string? itemName = null)
    {
        Update(_current + 1, itemName);
    }

    /// <summary>
    /// Updates progress to a specific count.
    /// </summary>
    /// <param name="count">Current count</param>
    /// <param name="itemName">Optional name of the current item being processed</param>
    public void Update(int count, string? itemName = null)
    {
        _current = count;
        
        if (_total <= 0) return;

        var percent = (int)((double)_current / _total * 100);
        
        // Only update if percent changed (reduces console output)
        if (percent == _lastPercent && _useBar) return;
        
        _lastPercent = percent;

        if (_useBar)
        {
            DisplayBar(percent, itemName);
        }
        else
        {
            DisplaySimple(percent, itemName);
        }
    }

    /// <summary>
    /// Marks progress as complete.
    /// </summary>
    public void Complete()
    {
        Update(_total);
        Console.WriteLine();
    }

    private void DisplayBar(int percent, string? itemName)
    {
        var filled = (int)((double)percent / 100 * _barWidth);
        var empty = _barWidth - filled;
        
        var bar = new StringBuilder();
        bar.Append('[');
        bar.Append(new string('=', filled));
        bar.Append(new string(' ', empty));
        bar.Append(']');
        
        var status = $"{bar} {percent,3}%";
        string output;
        
        if (!string.IsNullOrEmpty(itemName))
        {
            output = $"\r{_taskName}: {status} | {itemName}";
        }
        else
        {
            output = $"\r{_taskName}: {status}";
        }

        // Pad with spaces to clear any previous longer line
        Console.Write(output.PadRight(Console.WindowWidth > 0 ? Console.WindowWidth - 1 : 100));
    }

    private void DisplaySimple(int percent, string? itemName)
    {
        var status = $"{_current}/{_total} ({percent}%)";
        
        if (!string.IsNullOrEmpty(itemName))
        {
            Console.WriteLine($"{_taskName}: {status} - {itemName}");
        }
        else
        {
            Console.WriteLine($"{_taskName}: {status}");
        }
    }

    /// <summary>
    /// Creates a spinner for indeterminate progress.
    /// </summary>
    public static Spinner CreateSpinner(string message)
    {
        return new Spinner(message);
    }

    /// <summary>
    /// Spinner for indeterminate progress operations.
    /// </summary>
    public class Spinner : IDisposable
    {
        private readonly string _message;
        private readonly char[] _frames = { '|', '/', '-', '\\' };
        private int _currentFrame;
        private readonly Task _spinnerTask;
        private readonly CancellationTokenSource _cts;
        private bool _disposed;

        public Spinner(string message)
        {
            _message = message;
            _currentFrame = 0;
            _cts = new CancellationTokenSource();
            _spinnerTask = Task.Run(() => Run(_cts.Token));
        }

        private void Run(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                Console.Write($"\r{_message} {_frames[_currentFrame]}");
                _currentFrame = (_currentFrame + 1) % _frames.Length;
                Thread.Sleep(100);
            }
            
            // Clear the spinner when done
            Console.Write($"\r{_message} ✓");
            Console.WriteLine();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            try
            {
                _spinnerTask.Wait(500);
            }
            catch (AggregateException)
            {
                // Task was cancelled, which is expected
            }
            _cts.Dispose();
        }
    }
}
