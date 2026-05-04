namespace GoGameShop.Api.Shared.FileUpload;

public class FileUploader(IWebHostEnvironment Environment, IHttpContextAccessor httpContextAccessor)
{
    public async Task<FileUploadResult> UploadFileAsync(IFormFile? file, string folder)
    {
        var result = new FileUploadResult();

        // Validate if file is found
        if (file == null || file.Length == 0)
        {
            result.IsSuccess = false;
            result.ErrorMessage = "File not found";
            return result;
        }

        // Validate for file size (Less than 10 Mgb)
        if (file.Length > 10 * 1024 * 1024)
        {
            result.IsSuccess = false;
            result.ErrorMessage = "File size is too large";
            return result;
        }

        // Validate for supported file types
        string[] permittedExtensions = [".jpg", ".jpeg", ".png"];
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (string.IsNullOrEmpty(fileExtension) || !permittedExtensions.Contains(fileExtension))
        {
            result.IsSuccess = false;
            result.ErrorMessage = "Unsupported file type";
            return result;
        }

        var uploadFolder = Path.Combine(Environment.WebRootPath, folder);
        if (!Directory.Exists(uploadFolder))
            Directory.CreateDirectory(uploadFolder);
        var safeFileName = $"{Guid.NewGuid()}{fileExtension}";
        var fullPath = Path.Combine(uploadFolder, safeFileName);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);

        var httpContext = httpContextAccessor.HttpContext;
        var fileUrl =
            $"{httpContext?.Request.Scheme}://{httpContext?.Request.Host}/{folder}/{safeFileName}";

        result.IsSuccess = true;
        result.FileUrl = fileUrl;
        return result;
    }
}
