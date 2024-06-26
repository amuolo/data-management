﻿using Enterprise.MessageHub;

namespace Enterprise.Agency.Tests;

public class XHub : MessageHub<IContractAgentX>
{
    public async Task<XModel> CreateRequest()
    {
        await Task.Delay(10);
        return new("Paolo", "Rossi");
    }

    public XModel GetRequest(XModel model)
    {
        return model;
    }

    public async Task<XModel> GetRequestAsync(XModel model)
    {
        await Task.Delay(10);
        return GetRequest(model);
    }

    public void UpdateRequest(string name, XModel model)
    {
        if (name is null || name is "")
        {
            LogPost($"Cannot process null or empty");
            return;
        }

        model.Name = name;

        Post(agent => agent.Display, $"Data has been processed");
        Post(agent => agent.DataChangedEvent);
    }

    public async Task UpdateRequestAsync(string name, XModel model)
    {
        await Task.Delay(10);
        UpdateRequest(name, model);
    }

    public async Task SomeWorkAsync()
    {
        await Task.Delay(2000);
    }

    public async Task SomeWorkAsync(string a)
    {
        await Task.Delay(2000);
    }

    public async Task<int> SomeWorkWithResultAsync(int i)
    {
        await Task.Delay(2000);
        return ++i;
    }
}

public class YHub : MessageHub<IContractAgentY>
{
    public YModel CreateRequest()
    {
        return new("Bingo", "Bongo");
    }

    public double ValidateRequestWithDoubleReturn(string a)
    {
        return double.TryParse(a, out var res)? res + 0.5 : 0;
    }

    public YModel ValidateRequestWithObjectReturn(string a, YModel state)
    {
        return state with { Name = a };
    }
}
