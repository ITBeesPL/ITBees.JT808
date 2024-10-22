using ITBees.JT808.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JT808ServerApp.Setup;

public class JT808Setup : ITBees.FAS.Setup.IFasDependencyRegistration
{
    public void Register(IServiceCollection services, IConfigurationRoot configurationRoot)
    {
        if (services.Any(descriptor =>
                descriptor.ServiceType == typeof(IGpsDeviceAuthorizationSingleton)) == false)
        {
            throw new Exception(
                "You must implement and register IGpsDeviceAuthorizationSingleton interface for proper work fas payment module");
        };

        if (services.Any(descriptor =>
                descriptor.ServiceType == typeof(IGpsWriteRequestLogSingleton)) == false)
        {
            throw new Exception(
                "You must implement and register IGpsWriteRequestLogSingleton interface for proper work fas payment module");
        };

        if (string.IsNullOrEmpty(configurationRoot.GetSection("JT808_port").Value))
        {
            throw new Exception("Your config file has no entry called 'JT808_port'");
        }
    }

    public static void RegisterDbModels(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GpsData>();
        modelBuilder.Entity<UnauthorizedGpsDevice>().HasKey(x => x.Id);
        modelBuilder.Entity<UnauthorizedGpsDevice>().HasIndex(x => x.DeviceId).IsUnique();
        modelBuilder.Entity<UnauthorizedGpsDevice>(entity =>
        {
            entity.OwnsOne(e => e.LatestGpsLocation);
        });

        modelBuilder.Entity<GpsDevice>().HasKey(x => x.Guid);
        modelBuilder.Entity<GpsDevice>(entity =>
        {
            entity.OwnsOne(e => e.LatestGpsLocation);
        });
    }
}