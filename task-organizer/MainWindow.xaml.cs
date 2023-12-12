using System.Diagnostics;
using System.IO;
using System.Windows;
using task_organizer;

namespace TaskOrganizer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>

public record MainWindowState(string Path, DataManager Data);

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
        State = new(path, new DataManager());
    }

    private string? GetFileName() => ((FileInfo)InputFilePicker.SelectedItem)?.FullName.Replace(State.Path, "");

    private void ImportClick(object sender, RoutedEventArgs e)
    {
        var fileName = GetFileName();
        Task.Factory.StartNew(() => ReadFromDisk())
                    .ContinueWith(completed => UpdateListBox($"Import from file {fileName} took : " + completed.Result + " ms", 0));

        string ReadFromDisk()
        {
            if (fileName == null || fileName == "") MessageBox.Show(Messages.EmptyFileName);
            var timer = new Stopwatch();
            timer.Start();
            var a = "a" + fileName;
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
            File.WriteAllLines(fileName, State.Data.GetData());
            timer.Stop();
            return timer.ElapsedMilliseconds.ToString();
        }
    }

    private void OpenDataWindowClick(object sender, RoutedEventArgs e)
    {
        var dataWindow = new DataWindow();
        dataWindow.Show();
    }

    private void UpdateListBox(string msg, int index)
    {
        //Dispatcher.BeginInvoke(new Action(delegate () { MainArea.Items.Insert(index, msg); }));

        Dispatcher.BeginInvoke(() => MainArea.Items.Insert(index, msg));
    }
}