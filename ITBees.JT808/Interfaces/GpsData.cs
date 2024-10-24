﻿namespace ITBees.JT808.Interfaces;

public class GpsData
{
    public int Id { get; set; }
    public string DeviceId { get; set; }
    public string? VIN { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Speed { get; set; }
    public ushort Direction { get; set; }
    public DateTime Timestamp { get; set; }
    public uint AlarmFlag { get; set; }
    public uint Status { get; set; }
    public ushort Altitude { get; set; }
    public double Mileage { get; set; }
    public uint ExtendedStatus { get; set; }
    public ushort IOStatus { get; set; }
    public byte NetworkSignal { get; set; }
    public byte Satellites { get; set; }
    public double BatteryVoltage { get; set; }
    public GpsDevice GpsDevice { get; set; }
    public Guid? GpsDeviceGuid { get; set; }
    public string? RequestBody { get; set; }
    public DateTime Received { get; set; }
}