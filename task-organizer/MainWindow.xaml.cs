using System.Windows;
using task_organizer;

namespace TaskOrganizer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private DataManager DataManager { get; set; } = new DataManager();

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Wip1Click(object sender, RoutedEventArgs e)
    {
        List<string> lstInputs = new List<string>();

        var mergedInput = string.Join("\r\n\r\n", lstInputs);

        MessageBox.Show(mergedInput);
    }

    private void ImportClick(object sender, RoutedEventArgs e)
    {

    }

    private void ExportClick(object sender, RoutedEventArgs e)
    {
     
    }

    private void OpenSecondWindowClick(object sender, RoutedEventArgs e)
    {
        var secondWindow = new SecondWindow();
        secondWindow.Show();
    }
}