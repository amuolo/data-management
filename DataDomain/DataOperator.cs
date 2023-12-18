using System.Text.Json;

namespace DataDomain;

public static class DataOperator
{
    public static void Save(object data)
    {
        File.WriteAllText("storage", JsonSerializer.Serialize(data));
    }

    public static List<string> Import(FileInfo? file)
    {
        if (file == null) throw new Exception(Messages.EmptyFileName);

        switch (file.Extension)
        {
            case ".csv": 

                return File.ReadLines(file.FullName).SelectMany(str => str.Split(new[] { "\n" }, StringSplitOptions.None)).ToList(); 

            default: 
                
                throw new Exception(Messages.ExtensionNotHandled);
        }
    }

    public static void Export(List<string> data, string? fileName)
    {
        if (fileName == null) throw new Exception(Messages.EmptyFileName);

        var text = string.Join("\n", data);

        File.WriteAllText(fileName, text);
    }
}
