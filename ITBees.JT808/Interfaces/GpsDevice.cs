using ITBees.Models.Common;
using ITBees.Models.Companies;
using ITBees.Models.Users;

namespace ITBees.JT808.Interfaces;

public class GpsDevice
{
    public Guid Guid { get; set; }
    public string DeviceId { get; set; }
    public DateTime? LastConnection { get; set; }
    public Guid? CompanyGuid { get; set; }
    public Company Company { get; set; }
    public bool IsAllowed { get; set; }
    public UserAccount CreatedBy { get; set; }
    public GpsLocation? LatestGpsLocation { get; set; }
    public string PhoneNumber { get; set; }
}