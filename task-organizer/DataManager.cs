using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Printing;
using System.Text;
using System.Threading.Tasks;

namespace task_organizer;

public record DataManager
{
    public ImmutableList<string> Input { get; set; } = ImmutableList<string>.Empty;

    public ImmutableList<string> Refined { get; set; } = ImmutableList<string>.Empty;

    internal IEnumerable<string> GetData()
    {
        return Refined.ToArray();
    }
}
