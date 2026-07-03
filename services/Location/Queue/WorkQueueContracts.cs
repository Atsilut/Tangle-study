namespace Location.Queue;

public sealed record LocationClusterJob(
    decimal MinLatitude,
    decimal MaxLatitude,
    decimal MinLongitude,
    decimal MaxLongitude,
    int Zoom,
    int SchemaVersion = 1);
