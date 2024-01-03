using System.Windows;
using System.Windows.Controls;

namespace DataManagement;

/// <summary>
/// Message Box with Progress Bar
/// </summary>
public partial class ProgressBar : Window
{
    int progress;
    int totalNumSteps;

    internal ProgressBar()
    {
        InitializeComponent();
    }

    internal void Enable(int total)
    {
        Dispatcher.BeginInvoke(() => {
            totalNumSteps = total;
            progress = 1;
            Show();
        });
    }

    internal void Update()
    {
        Dispatcher.BeginInvoke(() =>
        {
            ProgressStatus.IsIndeterminate = false;
            ProgressStatus.Value = progress++ * 100 / totalNumSteps;
        });
    }

    internal void Disable()
    {
        Dispatcher.BeginInvoke(() => Close());
    }
}
