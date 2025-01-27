namespace ITBees.JT808.Interfaces;

public interface IGpsWriteRequestLogSingleton<T> where T: GpsData
{
    /// <summary>
    /// Saves log entry, and return log Id
    /// </summary>
    /// <param name="gpsData"></param>
    /// <returns></returns>
    int Write(T gpsData);
    int Write(List<T> gpsData);
    void Update (T gpsData);
    void UpdateHeartBeat(T gpsData);
    void ExtractedVin<T>(string vin, T gpsData);
    void WriteUnknownMessages<T>(T gpsData);
    void HandleTerminalRegistration<T>(T gpsData);
    void HandleAuthentication<T>(T gpsData);
    void WriteTimeSyncRequest<T>(T gpsData);
}