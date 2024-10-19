using ITBees.FAS.ApiInterfaces.Companies;
using ITBees.Models.Interfaces;

namespace ITBees.JT808.Interfaces;

public class GpsDeviceVm
{
    public GpsDeviceVm()
    {
        
    }
    public GpsDeviceVm(GpsDevice x)
    {
        Guid = x.Guid;
        DeviceId = x.DeviceId;
        LastConnection = x.LastConnection;
        CompanyGuid = x.CompanyGuid;
        IsAllowed = x.IsAllowed;
        CreatedBy = x.CreatedBy?.DisplayName;
        LatestGpsLocation = new GpsLocationVm(x.LatestGpsLocation);
        PhoneNumber = x.PhoneNumber;
    }

    public Guid Guid { get; set; }
    public string DeviceId { get; set; }
    public DateTime? LastConnection { get; set; }
    public Guid? CompanyGuid { get; set; }
    public CompanyVm Company { get; set; }
    public bool IsAllowed { get; set; }
    public string CreatedBy { get; set; }
    public GpsLocationVm? LatestGpsLocation { get; set; }
    public string PhoneNumber { get; set; }
}