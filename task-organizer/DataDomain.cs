using System.Collections.Immutable;
using System.Diagnostics;
using System.Windows.Media.TextFormatting;
namespace TaskOrganizer;

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

    internal void ReadFromDisk(string? fileName)
    {
        // TODO
    }

    internal void WriteToDisk(string? fileName)
    {
        // TODO
    }
}
