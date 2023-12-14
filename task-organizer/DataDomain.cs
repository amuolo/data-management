using System.IO;

namespace DataManagement;

public record DataDomain
{
    private List<string> Input { get; set; } = new();

    private List<string> Refined { get; set; } = new();

    internal List<string> GetData() 
    {
        // TODO
        return Refined;
    }

    internal void ProcessData()
    {
        // TODO
    }

    internal void ReadFromDisk(FileInfo? file)
    {
        if(file == null) throw new Exception(Messages.EmptyFileName);

        var text = File.ReadAllText(file.FullName);

        switch (file.Extension)
        {
            case ".csv":  Input = text.Split(new[] { "\n" }, StringSplitOptions.None).ToList();  break;

            default:  throw new Exception(Messages.ExtensionNotHandled);
        }
    }

    internal void WriteToDisk(string? fileName)
    {
        if (fileName == null) throw new Exception(Messages.EmptyFileName);

        var text = string.Join("\n", Refined);

        File.WriteAllText(fileName, text);
    }
}
