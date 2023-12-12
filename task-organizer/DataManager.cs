using System.Collections.Immutable;
using System.Diagnostics;
namespace TaskOrganizer;

public record DataManager
{
    public ImmutableList<string> Input { get; set; } = ImmutableList<string>.Empty;

    public ImmutableList<string> Refined { get; set; } = ImmutableList<string>.Empty;

    internal string ProcessData()
    {
        var timer = new Stopwatch();
        timer.Start();
        // TODO: finish
        Refined = Input;
        timer.Stop();
        return timer.ElapsedMilliseconds.ToString();
    }
}
