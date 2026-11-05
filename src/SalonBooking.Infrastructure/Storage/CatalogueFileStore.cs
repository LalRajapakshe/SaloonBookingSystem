using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SalonBooking.Application.Common;

namespace SalonBooking.Infrastructure.Storage;

public sealed class CatalogueFileStore
{
    private static readonly Regex StoredName = new(
        "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\\.(jpg|png|webp)$",
        RegexOptions.CultureInvariant);

    private readonly IHostEnvironment _environment;
    private readonly CataloguePhotoOptions _options;

    public CatalogueFileStore(IHostEnvironment environment, IOptions<CataloguePhotoOptions> options)
    {
        _environment = environment;
        _options = options.Value;
    }

    public async Task<string> SaveAsync(
        CatalogueImageKind kind,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        var extension = ImageSignatures.RequireExtension(content, _options.EffectiveMaxBytes);
        var fileName = $"{Guid.NewGuid()}.{extension}";
        var fullPath = CombineSafe(kind, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);
        return fileName;
    }

    public StoredFileContent? Open(CatalogueImageKind kind, string? storedName)
    {
        if (!IsStoredName(storedName))
        {
            return null;
        }

        var fullPath = CombineSafe(kind, storedName!);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.Asynchronous);
        return new StoredFileContent
        {
            Content = stream,
            ContentType = ImageSignatures.ContentType(storedName!)
        };
    }

    public void Delete(CatalogueImageKind kind, string? storedName)
    {
        if (!IsStoredName(storedName))
        {
            return;
        }

        try
        {
            var fullPath = CombineSafe(kind, storedName!);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (CustomerPhotoException)
        {
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private string CombineSafe(CatalogueImageKind kind, string fileName)
    {
        if (!IsStoredName(fileName))
        {
            throw new CustomerPhotoException("The stored photo reference is not valid.");
        }

        var root = Path.GetFullPath(Path.Combine(Root(), kind.ToString().ToLowerInvariant()));
        var fullPath = Path.GetFullPath(Path.Combine(root, fileName));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new CustomerPhotoException("The stored photo reference is not valid.");
        }

        return fullPath;
    }

    private string Root()
    {
        var configured = string.IsNullOrWhiteSpace(_options.StoragePath)
            ? "App_Data/catalogue-photos"
            : _options.StoragePath.Trim();
        var root = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(_environment.ContentRootPath, configured);
        return Path.GetFullPath(root);
    }

    private static bool IsStoredName(string? fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName) && StoredName.IsMatch(fileName);
    }
}
