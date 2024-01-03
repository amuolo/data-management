using DataDomain;
using Job;
using DataAgent;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace DataManagement;

public record MainWindowState(string Path, Model Data, DataWindow DataWindow);

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private MainWindowState State { get; set; }

    public MainWindow()
    {
        InitializeComponent();

        AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler((_, e) => Logger("Unhandled exception event"));

        Application.Current.DispatcherUnhandledException += new DispatcherUnhandledExceptionEventHandler((_, e) => Logger("Dispatcher Unhandled Exception"));

        TaskScheduler.UnobservedTaskException += new EventHandler<UnobservedTaskExceptionEventArgs>((_, e) => Logger("Unobserved Task Exception Event Args"));

        var dirInfo = new DirectoryInfo(".");
        var files = dirInfo.GetFiles("*.*", SearchOption.AllDirectories);
        InputFilePicker.ItemsSource = files;

        State = new(dirInfo.FullName, new Model(), new DataWindow());
    }

    private void OpenDataWindowClick(object sender, RoutedEventArgs e) => State.DataWindow.Show();

    private void Logger(string msg) => Dispatcher.BeginInvoke(() => MainArea.Items.Insert(0, msg));

    private void ImportClick(object sender, RoutedEventArgs e)
    {
        var file = ((FileInfo)InputFilePicker.SelectedItem);
        var fileName = file?.FullName?.Replace(State.Path, "");
        var progressBar = new ProgressBar();

        JobFactory.New().WithOptions(o => o.WithLogs(Logger).WithProgress(progressBar.Enable, progressBar.Update, progressBar.Disable))
                        .WithStep($"Import from file {fileName}", () => State.Data.Update(DataOperator.Import(file)))
                        .WithStep($"Processing new data", () => State.Data.Process())
                        .WithStep($"Update Data Window", () => State.DataWindow.Update(State.Data.GetPrintable()))
                        .Start();
    }

    private void ExportClick(object sender, RoutedEventArgs e)
    {
        var name = ExportFileName?.Text;
        var fileName = State.Path + "\\" + name?? "";
        var progressBar = new ProgressBar();

        JobFactory.New().WithOptions(o => o.WithLogs(Logger).WithProgress(progressBar.Enable, progressBar.Update, progressBar.Disable))
                        .WithStep($"Export to file {name}", () => DataOperator.Export(State.Data.GetPrintable(), fileName))
                        .Start();
    }
}