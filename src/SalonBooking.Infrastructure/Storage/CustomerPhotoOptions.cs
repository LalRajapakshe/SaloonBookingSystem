namespace SalonBooking.Infrastructure.Storage;

public sealed class CustomerPhotoOptions
{
    public const string SectionName = "CustomerPhotos";

    /// <summary>
    /// Relative paths are stored under the application content root.
    /// Production should set CustomerPhotos__StoragePath to a persistent folder
    /// outside the web root. The API process needs read, write, and delete
    /// permission there, and that folder should be included in backups.
    /// </summary>
    public string StoragePath { get; set; } = "App_Data/customer-photos";

    public long MaxBytes { get; set; } = 2_097_152;

    public long EffectiveMaxBytes
    {
        get
        {
            var configured = MaxBytes <= 0 ? 2_097_152 : MaxBytes;
            return Math.Min(configured, 5_242_880);
        }
    }
}
