using System.Diagnostics;
using System.IO;
using System.Windows;

namespace TaskOrganizer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>

public record MainWindowState(string Path, DataManager Data, DataWindow DataWindow);

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
        State = new(path, new DataManager(), new DataWindow());
    }

    private void OpenDataWindowClick(object sender, RoutedEventArgs e) => State.DataWindow.Show();

    private string? GetFileName() => ((FileInfo)InputFilePicker.SelectedItem)?.FullName.Replace(State.Path, "");

    private void UpdateListBox(string msg, int index) => Dispatcher.BeginInvoke(() => MainArea.Items.Insert(index, msg));

    private void ImportClick(object sender, RoutedEventArgs e)
    {
        var fileName = GetFileName();
        Task.Factory.StartNew(() => ReadFromDisk())
                    .ContinueWith(completed => UpdateListBox($"Import from file {fileName} took : " + completed.Result + " ms", 0))
                    .ContinueWith(completed => State.Data.ProcessData())
                    .ContinueWith(completed => UpdateListBox($"Processing new data took : " + completed.Result + " ms", 0))
                    .ContinueWith(completed => State.DataWindow.Update(State.Data.Refined));

        string ReadFromDisk()
        {
            if (fileName == null || fileName == "") MessageBox.Show(Messages.EmptyFileName);
            var timer = new Stopwatch();
            timer.Start();
            // TODO: finish
            timer.Stop();
            return timer.ElapsedMilliseconds.ToString();
        }
    }

    private void ExportClick(object sender, RoutedEventArgs e)
    {
        var fileName = GetFileName();
        Task.Factory.StartNew(() => WriteToDisk())
                    .ContinueWith(completed => UpdateListBox($"Export to file {fileName} took : " + completed.Result + " ms", 0));

        string WriteToDisk()
        {
            if (fileName == null || fileName == "") MessageBox.Show(Messages.EmptyFileName);
            File.Delete(fileName);
            var timer = new Stopwatch();
            timer.Start();
            File.WriteAllLines(fileName, State.Data.Refined);
            timer.Stop();
            return timer.ElapsedMilliseconds.ToString();
        }
    }
}