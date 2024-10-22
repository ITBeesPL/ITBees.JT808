using ITBees.JT808.Interfaces;

namespace ITBees.JT808.DefaultServiceImplementation;

public class GpsDeviceAuthorizationSingleton : IGpsDeviceAuthorizationSingleton
{
    public bool IsAuthorized(string deviceId, string terminalModel, string terminalId, string gpsDataVin)
    {
        throw new NotImplementedException();
    }
}