namespace Api.Domain.Media.Domain;

public enum MediaProcessingStatus
{
    PendingUpload = 0,
    Processing = 1,
    Ready = 2,
    Failed = 3,
}
