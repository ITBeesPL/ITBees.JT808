namespace ITBees.JT808.Interfaces;

public interface IGpsWriteRequestLogSingleton<T> where T: GpsData
{
    /// <summary>
    /// Saves log entry, and return log Id
    /// </summary>
    /// <param name="gpsData"></param>
    /// <returns></returns>
    int Write(T gpsData);
    void Update (T gpsData);
    void UpdateHeartBeat(T gpsData);
}