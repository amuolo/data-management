using System.Text.Json;

namespace Data.Agent;

public static class DataOperator
{
    public static object? Load()
    {
        return JsonSerializer.Deserialize<object>(File.ReadAllText("storage"));
    }

    public static void Save(object data)
    {
        File.WriteAllText("storage", JsonSerializer.Serialize(data));
    }

    public static List<string> Import(FileInfo? file)
    {
        if (file == null) throw new Exception(Logs.EmptyFileName);

        switch (file.Extension)
        {
            case ".csv":
                return File.ReadLines(file.FullName).SelectMany(str => str.Split(new[] { "\n" }, StringSplitOptions.None)).ToList();

            default:
                throw new Exception(Logs.ExtensionNotHandled);
        }
    }

    public static void Export(List<string> data, string? fileName)
    {
        if (fileName == null || fileName == "" || fileName.EndsWith("\\")) throw new Exception(Logs.EmptyFileName);

        var text = string.Join("\n", data);

        File.WriteAllText(fileName, text);
    }
}
