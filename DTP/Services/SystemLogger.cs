using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;

namespace DTP.Services;

public static class SystemLogger
{
    public static ObservableCollection<string> Messages { get; } = new();

    public static void Add(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string logEntry = $"[{timestamp}] {message}";

        Dispatcher.UIThread.Post(() =>
        {
            Messages.Insert(0, logEntry);
        });

        Console.WriteLine(logEntry);
    }
}