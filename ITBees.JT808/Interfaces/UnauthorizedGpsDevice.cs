using ITBees.Models.Common;

namespace ITBees.JT808.Interfaces;

public class UnauthorizedGpsDevice
{
    public int Id { get; set; }
    public string DeviceId { get; set; }
    public DateTime? LastConnection { get; set; }
    public int ConnectionCount { get; set; }
    public GpsLocation? LatestGpsLocation { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Vin { get; set; }
    public string? TerminalId { get; set; }
    public string? TerminalModel { get; set; }
}