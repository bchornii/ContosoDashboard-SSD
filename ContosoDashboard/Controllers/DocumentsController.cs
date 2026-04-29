using ContosoDashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ContosoDashboard.Controllers
{
    [ApiController]
    [Route("documents")]
    [Authorize]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentService _documentService;

        public DocumentsController(IDocumentService documentService)
        {
            _documentService = documentService;
        }

        [HttpGet("download/{id:int}")]
        public async Task<IActionResult> Download(int id)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized();

            var result = await _documentService.DownloadDocumentAsync(id, userId);
            if (!result.Success || result.FileStream == null)
                return NotFound();

            return File(result.FileStream, result.ContentType, result.FileName);
        }

        [HttpGet("preview/{id:int}")]
        public async Task<IActionResult> Preview(int id)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized();

            var result = await _documentService.GetPreviewAsync(id, userId);
            if (!result.Success || result.FileStream == null)
                return NotFound();

            // Inline disposition for preview
            Response.Headers.Append("Content-Disposition", $"inline; filename=\"{result.FileName}\"");
            return File(result.FileStream, result.ContentType);
        }
    }
}
