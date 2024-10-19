using ITBees.JT808.Interfaces;

namespace ITBees.JT808.DefaultServiceImplementation;

public class GpsDeviceAuthorizationSingleton : IGpsDeviceAuthorizationSingleton
{
    public bool IsAuthorized(string deviceId)
    {
        throw new NotImplementedException();
    }
}