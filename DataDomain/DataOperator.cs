namespace DataDomain;

public record DataOperator
{
    private List<string> Input { get; set; } = new();

    private List<string> Refined { get; set; } = new();

    public List<string> GetData()
    {
        // TODO
        return Refined;
    }

    public void ProcessData()
    {
        // TODO
    }

    public void ReadFromDisk(FileInfo? file)
    {
        if (file == null) throw new Exception(Messages.EmptyFileName);

        switch (file.Extension)
        {
            case ".csv": 

                Input = File.ReadLines(file.FullName).SelectMany(str => str.Split(new[] { "\n" }, StringSplitOptions.None)).ToList(); 
                break;

            default: 
                
                throw new Exception(Messages.ExtensionNotHandled);
        }
    }

    public void WriteToDisk(string? fileName)
    {
        if (fileName == null) throw new Exception(Messages.EmptyFileName);

        var text = string.Join("\n", Refined);

        File.WriteAllText(fileName, text);
    }
}
