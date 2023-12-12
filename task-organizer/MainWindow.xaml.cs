using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using task_organizer;

namespace TaskOrganizer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
 
public record MainWindowState(DataManager Data);

public partial class MainWindow : Window
{
    private MainWindowState State { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        InputFilePicker.ItemsSource = typeof(Colors).GetProperties();
        State = new(new DataManager());
    }

    private void ImportClick(object sender, RoutedEventArgs e)
    {

    }

    private void ExportClick(object sender, RoutedEventArgs e)
    {
        Task.Factory.StartNew(() => WriteToDisk())
                    .ContinueWith(completed => Logging(completed.Result));

        string WriteToDisk()
        {
            var fileName = State.Data.FileName;
            if (fileName == null || fileName == "") MessageBox.Show(Messages.EmptyFileName);
            File.Delete(fileName);
            Stopwatch timer = new Stopwatch();
            timer.Start();
            File.WriteAllLines(fileName, State.Data.GetData());
            timer.Stop();
            return timer.ElapsedMilliseconds.ToString();
        }

        void Logging(string time) => UpdateListBox("Export to file took : " + time + " ms", 0);
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