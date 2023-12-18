using System.Windows;

namespace DataManagement;

/// <summary>
/// Interaction logic for SecondWindow.xaml
/// </summary>
public record DataWindowState();

public partial class DataWindow : Window
{
    private DataWindowState State { get; set; }

    internal DataWindow()
    {
        InitializeComponent();
        State = new();
    }

    internal void Update(List<string> data)
    {
        Dispatcher.BeginInvoke(() => data.ForEach(x => DataArea.Items.Insert(0, x)));
    }
}
