namespace ITBees.JT808.Interfaces;

public interface IJT808Server<T> where T : class
{
    Task StartAsync();
}