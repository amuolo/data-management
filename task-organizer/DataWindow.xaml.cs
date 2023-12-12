using System.Collections.Immutable;
using System.Windows;
namespace TaskOrganizer;

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

    public void Update(ImmutableList<string> data)
    {
        data.ForEach(x => UpdateListBox(x, 0));
    }

    private void UpdateListBox(string msg, int index) => Dispatcher.BeginInvoke(() => DataArea.Items.Insert(index, msg));
}
