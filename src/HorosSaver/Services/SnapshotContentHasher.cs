using System.IO.Compression;
using System.Security.Cryptography;
using HorosSaver.Models;

namespace HorosSaver.Services;

internal static class SnapshotContentHasher
{
    public const long HashSizeLimitBytes = 50 * 1024 * 1024;
    private const int CompressionThresholdBytes = 512;

    public static string? TryComputeHash(string absolutePath)
    {
        if (!File.Exists(absolutePath))
        {
            return null;
        }

        try
        {
            var info = new FileInfo(absolutePath);
            if (info.Length > HashSizeLimitBytes)
            {
                return null;
            }

            using var stream = File.OpenRead(absolutePath);
            var hash = SHA256.HashData(stream);
            return Convert.ToHexString(hash)[..12];
        }
        catch (IOException)
        {
            return null;
        }
    }

    public static bool ShouldCompress(bool compressionEnabled, long sizeBytes)
        => compressionEnabled && sizeBytes >= CompressionThresholdBytes;

    public static async Task WriteCompressedCopyAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        await using var sourceStream = File.OpenRead(sourcePath);
        await using var destinationStream = File.Create(destinationPath);
        await using var gzipStream = new GZipStream(destinationStream, CompressionLevel.Optimal);
        await sourceStream.CopyToAsync(gzipStream, cancellationToken).ConfigureAwait(false);
    }

    public static void DecompressToFile(string compressedPath, string destinationPath)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        using var sourceStream = File.OpenRead(compressedPath);
        using var gzipStream = new GZipStream(sourceStream, CompressionMode.Decompress);
        using var destinationStream = File.Create(destinationPath);
        gzipStream.CopyTo(destinationStream);
    }

    public static string NormalizeRelativePath(string relativePath)
        => relativePath.Replace('\\', '/').TrimStart('/');
}
