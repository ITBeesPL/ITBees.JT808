using ITBees.JT808.Interfaces;

namespace ITBees.JT808.DefaultServiceImplementation;

public class GpsWriteRequestLogSingleton<T> : IGpsWriteRequestLogSingleton<T> where T : GpsData
{
    public int Write(T gpsData)
    {
        throw new NotImplementedException();
    }

    public void Update(T gpsData)
    {
        throw new NotImplementedException();
    }

    public void UpdateHeartBeat(T gpsData)
    {
        throw new NotImplementedException();
    }
}