namespace ITBees.JT808.Interfaces;

public interface IGpsWriteRequestLogSingleton
{
    /// <summary>
    /// Saves log entry, and return log Id
    /// </summary>
    /// <param name="gpsData"></param>
    /// <returns></returns>
    int Write(GpsData gpsData);
    void Update (GpsData gpsData);
    void UpdateHeartBeat(GpsData gpsData);
}