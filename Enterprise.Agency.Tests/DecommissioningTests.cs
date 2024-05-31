using System.Collections.Concurrent;

namespace Enterprise.Agency.Tests;

[TestClass]
public class DecommissioningTests
{
    readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);


    [TestMethod]
    public async Task AgentDecommissioning()
    {
        var storage = new ConcurrentBag<Log>();
    }
}
