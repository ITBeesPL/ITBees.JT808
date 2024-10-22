namespace ITBees.JT808.Interfaces;

public interface IGpsDeviceAuthorizationSingleton
{
    bool IsAuthorized(string deviceId, string terminalModel, string terminalId, string gpsDataVin);
}