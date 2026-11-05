namespace SalonBooking.Infrastructure.Storage;

internal static class ImageSignatures
{
    public static string RequireExtension(byte[] content, long maxBytes)
    {
        if (content.Length == 0)
        {
            throw new Application.Common.CustomerPhotoException("A photo file is required.");
        }

        if (content.Length > maxBytes)
        {
            throw new Application.Common.CustomerPhotoException("The photo is larger than the allowed size.");
        }

        if (IsJpeg(content))
        {
            return "jpg";
        }

        if (IsPng(content))
        {
            return "png";
        }

        if (IsWebp(content))
        {
            return "webp";
        }

        throw new Application.Common.CustomerPhotoException("The photo must be a JPEG, PNG, or WebP image.");
    }

    public static string ContentType(string fileName)
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

        return content[^8] == (byte)'I'
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
}
