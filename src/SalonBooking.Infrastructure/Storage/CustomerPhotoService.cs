using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SalonBooking.Application.Common;
using SalonBooking.Application.Features.Customer.DTOs;
using SalonBooking.Application.Interfaces;
using SalonBooking.Infrastructure.Security;
using SalonBooking.Persistence.Context;
namespace SalonBooking.Infrastructure.Storage;

public sealed class CustomerPhotoService : ICustomerPhotoService
{
    private static readonly System.Text.RegularExpressions.Regex StoredName = new(
        "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\\.(jpg|png|webp)$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private readonly SalonBookingDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IHostEnvironment _environment;
    private readonly CustomerPhotoOptions _options;

    public CustomerPhotoService(
        SalonBookingDbContext context,
        ICurrentUserService currentUser,
        IHostEnvironment environment,
        IOptions<CustomerPhotoOptions> options)
    {
        _context = context;
        _currentUser = currentUser;
        _environment = environment;
        _options = options.Value;
    }

    public async Task<CustomerResponse> SaveAsync(
        long customerId,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        var customer = await RequireCustomerAsync(customerId, cancellationToken);
        var inspected = Inspect(content);
        var previous = customer.PhotoPath;
        var fileName = $"{Guid.NewGuid()}.{inspected.Extension}";
        var fullPath = CombineSafe(fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);
        customer.PhotoPath = fileName;
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            customer.PhotoPath = previous;
            DeleteIfSafe(fileName);
            throw;
        }

        if (!string.Equals(previous, fileName, StringComparison.Ordinal))
        {
            DeleteIfSafe(previous);
        }

        return CustomerResponse.From(customer);
    }

    public async Task<CustomerPhotoContent?> OpenAsync(
        long customerId,
        CancellationToken cancellationToken = default)
    {
        var customer = await RequireCustomerAsync(customerId, cancellationToken);
        if (!IsStoredName(customer.PhotoPath))
        {
            return null;
        }

        var fullPath = CombineSafe(customer.PhotoPath!);
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
        return new CustomerPhotoContent
        {
            Content = stream,
            ContentType = ContentType(customer.PhotoPath!)
        };
    }

    public async Task<CustomerResponse> RemoveAsync(
        long customerId,
        CancellationToken cancellationToken = default)
    {
        var customer = await RequireCustomerAsync(customerId, cancellationToken);
        var previous = customer.PhotoPath;
        customer.PhotoPath = null;
        await _context.SaveChangesAsync(cancellationToken);
        DeleteIfSafe(previous);
        return CustomerResponse.From(customer);
    }

    private async Task<Domain.Entities.Customer> RequireCustomerAsync(
        long customerId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.RequireTenantId();
        return await _context.Customers.RequireOwnedAsync(
            _currentUser,
            item => item.CustomerId == customerId &&
                    item.TenantId == tenantId &&
                    !item.IsDeleted,
            item => item.BranchId,
            cancellationToken);
    }

    private InspectedImage Inspect(byte[] content)
    {
        if (content.Length == 0)
        {
            throw new CustomerPhotoException("A photo file is required.");
        }

        if (content.Length > _options.EffectiveMaxBytes)
        {
            throw new CustomerPhotoException("The photo is larger than the allowed size.");
        }

        if (IsJpeg(content))
        {
            return new InspectedImage("jpg");
        }

        if (IsPng(content))
        {
            return new InspectedImage("png");
        }

        if (IsWebp(content))
        {
            return new InspectedImage("webp");
        }

        throw new CustomerPhotoException("The photo must be a JPEG, PNG, or WebP image.");
    }

    private static bool IsJpeg(byte[] content)
    {
        if (content.Length < 4 || content[0] != 0xFF || content[1] != 0xD8 || content[2] != 0xFF)
        {
            return false;
        }

        var index = 2;
        var sawFrame = false;
        while (index + 1 < content.Length)
        {
            if (content[index] != 0xFF)
            {
                return false;
            }

            while (index < content.Length && content[index] == 0xFF)
            {
                index++;
            }

            if (index >= content.Length)
            {
                return false;
            }

            var marker = content[index++];
            if (marker == 0xD9)
            {
                return sawFrame;
            }

            if (marker == 0xD8 || marker is >= 0xD0 and <= 0xD7)
            {
                continue;
            }

            if (index + 1 >= content.Length)
            {
                return false;
            }

            var length = (content[index] << 8) + content[index + 1];
            if (length < 2 || index + length > content.Length)
            {
                return false;
            }

            if (marker is 0xC0 or 0xC1 or 0xC2)
            {
                if (length < 7)
                {
                    return false;
                }

                var height = (content[index + 3] << 8) + content[index + 4];
                var width = (content[index + 5] << 8) + content[index + 6];
                if (width <= 0 || height <= 0)
                {
                    return false;
                }

                sawFrame = true;
            }

            if (marker == 0xDA)
            {
                if (!sawFrame)
                {
                    return false;
                }

                index += length;
                return EndsWithJpegImage(content, index);
            }

            index += length;
        }

        return false;
    }

    private static bool EndsWithJpegImage(byte[] content, int index)
    {
        while (index + 1 < content.Length)
        {
            if (content[index] != 0xFF)
            {
                index++;
                continue;
            }

            var marker = content[index + 1];
            if (marker == 0x00 || marker is >= 0xD0 and <= 0xD7)
            {
                index += 2;
                continue;
            }

            return marker == 0xD9;
        }

        return false;
    }

    private static bool IsPng(byte[] content)
    {
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (content.Length < 24 || !content.AsSpan(0, 8).SequenceEqual(signature))
        {
            return false;
        }

        if (content[12] != (byte)'I' || content[13] != (byte)'H' || content[14] != (byte)'D' || content[15] != (byte)'R')
        {
            return false;
        }

        var width = (content[16] << 24) | (content[17] << 16) | (content[18] << 8) | content[19];
        var height = (content[20] << 24) | (content[21] << 16) | (content[22] << 8) | content[23];
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        return content.Length >= 12
            && content[^8] == (byte)'I'
            && content[^7] == (byte)'E'
            && content[^6] == (byte)'N'
            && content[^5] == (byte)'D';
    }

    private static bool IsWebp(byte[] content)
    {
        if (content.Length < 16)
        {
            return false;
        }

        if (content[0] != (byte)'R' || content[1] != (byte)'I' || content[2] != (byte)'F' || content[3] != (byte)'F')
        {
            return false;
        }

        if (content[8] != (byte)'W' || content[9] != (byte)'E' || content[10] != (byte)'B' || content[11] != (byte)'P')
        {
            return false;
        }

        var chunk = System.Text.Encoding.ASCII.GetString(content, 12, 4);
        return chunk is "VP8 " or "VP8L" or "VP8X";
    }

    private string CombineSafe(string fileName)
    {
        if (!IsStoredName(fileName))
        {
            throw new CustomerPhotoException("The stored photo reference is not valid.");
        }

        var root = Root();
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
            ? "App_Data/customer-photos"
            : _options.StoragePath.Trim();
        var root = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(_environment.ContentRootPath, configured);
        return Path.GetFullPath(root);
    }

    private void DeleteIfSafe(string? fileName)
    {
        if (!IsStoredName(fileName))
        {
            return;
        }

        try
        {
            var fullPath = CombineSafe(fileName!);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (CustomerPhotoException)
        {
            // A stored name that cannot be resolved is left in place.
        }
        catch (IOException)
        {
            // The customer record already points at the replacement.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool IsStoredName(string? fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName) && StoredName.IsMatch(fileName);
    }

    private static string ContentType(string fileName)
    {
        if (fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return "image/png";
        }

        if (fileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
        {
            return "image/webp";
        }

        return "image/jpeg";
    }

    private readonly record struct InspectedImage(string Extension);
}
