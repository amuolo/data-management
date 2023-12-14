using System.Collections.Immutable;
using System.Windows;

namespace DataManagement;

/// <summary>
/// Interaction logic for SecondWindow.xaml
/// </summary>

public record DataWindowState();

public partial class DataWindow : Window
{
    private DataWindowState State { get; set; }

    public DataWindow()
    {
        InitializeComponent();
        State = new();
    }

    public void Update(List<string> data)
    {
        Dispatcher.BeginInvoke(() => data.ForEach(x => DataArea.Items.Insert(0, x)));
    }
}
