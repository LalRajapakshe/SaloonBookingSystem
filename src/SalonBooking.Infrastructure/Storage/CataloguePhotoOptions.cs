namespace SalonBooking.Infrastructure.Storage;

public sealed class CataloguePhotoOptions
{
    public const string SectionName = "CataloguePhotos";

    public string StoragePath { get; set; } = "App_Data/catalogue-photos";

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

public enum CatalogueImageKind
{
    Branches,
    Services,
    Employees,
    Certificates
}
