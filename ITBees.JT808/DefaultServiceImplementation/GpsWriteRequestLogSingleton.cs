using ITBees.JT808.Interfaces;

namespace ITBees.JT808.DefaultServiceImplementation;

public class GpsWriteRequestLogSingleton : IGpsWriteRequestLogSingleton
{
    public void Update(GpsData gpsData)
    {
        throw new NotImplementedException();
    }

    public void UpdateHeartBeat(GpsData gpsData)
    {
        throw new NotImplementedException();
    }

    int IGpsWriteRequestLogSingleton.Write(GpsData gpsData)
    {
        throw new NotImplementedException();
    }
}