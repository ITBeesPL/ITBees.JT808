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

    public void ExtractedVin<T1>(string vin, T1 gpsData)
    {
        throw new NotImplementedException();
    }

    public void WriteUnknownMessages<T1>(T1 gpsData)
    {
        throw new NotImplementedException();
    }

    public void HandleTerminalRegistration<T1>(T1 gpsData)
    {
        throw new NotImplementedException();
    }

    public void HandleAuthentication<T1>(T1 gpsData)
    {
        throw new NotImplementedException();
    }

    public void WriteTimeSyncRequest<T1>(T1 gpsData)
    {
        throw new NotImplementedException();
    }
}