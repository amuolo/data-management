using System.Windows;

namespace App.WPF;

/// <summary>
/// Message Box with Progress Bar
/// </summary>
public partial class ProgressBar : Window
{
    internal ProgressBar()
    {
        InitializeComponent();
    }

    internal void Enable()
    {
        Dispatcher.BeginInvoke(() => Show());
    }

    internal void Update(double progress)
    {
        Dispatcher.BeginInvoke(() =>
        {
            ProgressStatus.IsIndeterminate = false;
            ProgressStatus.Value = progress * 100;
            if (progress == 100) Disable();
        });
    }

    internal void Disable()
    {
        Dispatcher.BeginInvoke(() => Close());
    }
}
