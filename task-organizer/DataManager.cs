using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace task_organizer;

class DataManager
{
    public ImmutableList<string> Input { get; set; } = ImmutableList<string>.Empty;
}
