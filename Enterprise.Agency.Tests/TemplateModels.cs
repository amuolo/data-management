using Enterprise.MessageHub;

namespace Enterprise.Agency.Tests;

public record Log(string Sender, string Message);

public record XModel()
{
    public string Name { get; set; } = "";
    public string Surname { get; set; } = "";

    public XModel(string name, string surname) : this()
    {
        Name = name;
        Surname = surname;
    }
}

public record YModel(string Name, string Surname);

