namespace ITBees.JT808.Interfaces;

public class TripDataPoint
{
    public int GpsDataId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Speed { get; set; }
    public double Mileage { get; set; }
    public ushort Altitude { get; set; }
    public DateTime Timestamp { get; set; }
    public ushort Direction { get; set; }
}