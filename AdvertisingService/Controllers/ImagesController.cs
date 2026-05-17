using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace AdvertisingService.Controllers;

[ApiController]
[Route("api/advertisementimages")]
public class AdvertisementImagesController : ControllerBase
{
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

    [HttpGet("advertisements/{fileName}")]
    public IActionResult GetAdvertisementImage(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return NotFound();
        }

        fileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return NotFound();
        }

        var webRootPath = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootPath
            ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        var physicalPath = Path.Combine(webRootPath, "uploads", "advertisements", fileName);
        var rootPath = Path.GetFullPath(Path.Combine(webRootPath, "uploads", "advertisements"));
        var fullPath = Path.GetFullPath(physicalPath);

        if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(fullPath))
        {
            return NotFound();
        }

        if (!ContentTypeProvider.TryGetContentType(fullPath, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return PhysicalFile(fullPath, contentType);
    }
}
