namespace ITBees.JT808;

public interface IJT808Server
{
    Task StartListening();
    void Stop();
}