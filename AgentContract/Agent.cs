using Job;
using System.Collections.Concurrent;

namespace Agency;

public class Agent<TState> where TState : new()
{
    private readonly ConcurrentQueue<IMessages> messages = new();

    private readonly Job<TState> job = JobFactory.New<TState>();


    public static Agent<TState> Create() => new();

}
