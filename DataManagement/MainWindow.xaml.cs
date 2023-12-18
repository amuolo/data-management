using DataDomain;
using JobAgent;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace DataManagement;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>

public record MainWindowState(string Path, DataOperator Data, DataWindow DataWindow);

public partial class MainWindow : Window
{
    private MainWindowState State { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        var dirInfo = new DirectoryInfo(".");
        var path = dirInfo.FullName;
        var files = dirInfo.GetFiles("*.*", SearchOption.AllDirectories);
        InputFilePicker.ItemsSource = files;
        State = new(path, new DataOperator(), new DataWindow());
    }

    private void OpenDataWindowClick(object sender, RoutedEventArgs e) => State.DataWindow.Show();

    private void Logger(string msg) => Dispatcher.BeginInvoke(() => MainArea.Items.Insert(0, msg));

    private void ImportClick(object sender, RoutedEventArgs e)
    {
        var file = ((FileInfo)InputFilePicker.SelectedItem);
        var fileName = file?.FullName?.Replace(State.Path, "");

        Job.New().WithOptions(o => o.WithLogs(Logger))
                 .WithStep($"Import from file {fileName}", () => State.Data.ReadFromDisk(file))
                 .WithStep($"Processing new data", () => State.Data.ProcessData())
                 .WithStep($"Update Data Window", () => State.DataWindow.Update(State.Data.GetData()))
                 .Start();
    }

    private void ExportClick(object sender, RoutedEventArgs e)
    {
        var name = ExportFileName?.Text;
        var fileName = State.Path + "\\" + name?? "";

        Job.New().WithOptions(o => o.WithLogs(Logger))
                 .WithStep($"Export to file {name}", () => State.Data.WriteToDisk(fileName))
                 .Start();
    }
}