using DatabaseClass.Models;
using Donation.ViewModels.Campaign;

namespace Donation.Services;

public class FileService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<FileService> _logger;

    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    private const long MaxImageSize = 5 * 1024 * 1024; // 5MB
    private const long MaxDocumentSize = 10 * 1024 * 1024; // 10MB

    public FileService(IWebHostEnvironment env, ILogger<FileService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public (bool Valid, string Error) ValidateImageFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return (false, "No file selected.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext))
            return (false, "Invalid image format. Allowed: JPG, PNG, WEBP, GIF.");

        if (file.Length > MaxImageSize)
            return (false, "Image size must be less than 5MB.");

        return (true, string.Empty);
    }

    public (bool Valid, string Error) ValidateDocumentFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return (false, "No file selected.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext))
            return (false, "Document must be an image file (JPG, PNG, WEBP, GIF).");

        if (file.Length > MaxDocumentSize)
            return (false, "Document size must be less than 10MB.");

        return (true, string.Empty);
    }

    public async Task<string> SaveImageAsync(IFormFile file, string subfolder)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", subfolder);
        Directory.CreateDirectory(uploadsFolder);

        string uniqueFileName = $"{Guid.NewGuid():N}{ext}";
        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return $"/uploads/{subfolder}/{uniqueFileName}";
    }

    public void DeleteFile(string? fileUrl)
    {
        if (string.IsNullOrEmpty(fileUrl) || !fileUrl.StartsWith("/uploads/"))
            return;

        try
        {
            string filePath = Path.Combine(_env.WebRootPath, fileUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete file: {FileUrl}", fileUrl);
        }
    }
}
