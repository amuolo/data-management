using Data.Domain;
using Data.Agent;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.AspNetCore.Components;
using Enterprise.Agency;

namespace App.WPF;

public interface IApp : IDataContract { }

public record MainWindowState(string Path, DataWindow DataWindow, ProgressBar ProgressBar);

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private MainWindowState State { get; set; }

    private Office<IApp> Office { get; }

    public MainWindow()
    {
        InitializeComponent();

        AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler((s, e) => Logger($"Unhandled exception event"));
        Application.Current.DispatcherUnhandledException += new DispatcherUnhandledExceptionEventHandler((s, e) => Logger($"Dispatcher Unhandled Exception"));
        TaskScheduler.UnobservedTaskException += new EventHandler<UnobservedTaskExceptionEventArgs>((s, e) => Logger($"Unobserved Task Exception Event"));

        // TODO: improve this file picker
        var dirInfo = new DirectoryInfo(".");
        var files = dirInfo.GetFiles("*.*", SearchOption.AllDirectories);
        InputFilePicker.ItemsSource = files;

        Office = Office<IApp>.Create()
                             .Register(agent => agent.DataChangedEvent, DataUpdate)
                             .Register(agent => agent.ShowProgress, ShowProgress)
                             .Register(agent => agent.Display, Logger)
                             .AddAgent<Model, DataHub, IDataContract>().Run();

        State = new(dirInfo.FullName, new DataWindow(), null);
    }

    private void OpenDataWindowClick(object sender, RoutedEventArgs e) => State.DataWindow.Show();

    private Action<string> Logger => (string msg) => Dispatcher.BeginInvoke(() => MainArea.Items.Insert(0, msg));

    private string? GetSelectedFile() => ((FileInfo)InputFilePicker.SelectedItem)?.FullName?.Replace(State.Path, "");

    private void DataUpdate()
    {
        Office.PostWithResponse<List<string>>(agent => agent.ReadRequest, Callback);

        void Callback(List<string> data) {
            if (data is not null)
            {
                State.DataWindow.Update(data);
                Logger($"Update Data Window");
            }
        }
    }

    private Action<double> ShowProgress => (double progress) =>
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (State.ProgressBar is null)
                State = State with { ProgressBar = new ProgressBar() };
            State.ProgressBar.Update(progress);
        });
    };

    private void ImportClick(object sender, RoutedEventArgs e)
    {
        Office.Post(agent => agent.ImportRequest, GetSelectedFile());

        /* This is the old implementation without actor model
        JobFactory.New().WithOptions(o => o.WithLogs(Logger).WithProgress(progressBar.Enable, progressBar.Update, progressBar.Disable))
                        .WithStep($"Import from file {fileName}", () => State.Data.Update(DataOperator.Import(file)))
                        .WithStep($"Processing new data", () => State.Data.Process())
                        .WithStep($"Update Data Window", () => State.DataWindow.Update(State.Data.GetPrintable()))
                        .Start();
        */
    }

    private void ExportClick(object sender, RoutedEventArgs e)
    {
        /* TODO: implement this
        JobFactory.New().WithOptions(o => o.WithLogs(Logger).WithProgress(progressBar.Enable, progressBar.Update, progressBar.Disable))
                        .WithStep($"Export to file {name}", () => DataOperator.Export(State.Data.GetPrintable(), fileName))
                        .Start();
        */
    }

    private void ConnectClick(object sender, RoutedEventArgs e)
    {

    }
}