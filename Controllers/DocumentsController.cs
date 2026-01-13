using Microsoft.AspNetCore.Mvc;
using PhysicianSignatureAPI.Services;

namespace PhysicianSignatureAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public DocumentsController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpGet]
    public async Task<IActionResult> GetDocuments([FromQuery] string? physicianId)
    {
        List<Models.Document> documents;
        
        if (!string.IsNullOrEmpty(physicianId))
        {
            documents = await _documentService.GetDocumentsByPhysicianIdAsync(physicianId);
        }
        else
        {
            documents = await _documentService.GetAllDocumentsAsync();
        }
        
        return Ok(documents);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDocument(string id)
    {
        var document = await _documentService.GetDocumentByIdAsync(id);
        if (document == null)
            return NotFound();

        return Ok(document);
    }

    [HttpGet("{id}/pdf")]
public async Task<IActionResult> GetPdf(string id)
{
    Console.WriteLine($"PDF request received for document ID: {id}");

    var document = await _documentService.GetDocumentByIdAsync(id);
    if (document == null)
    {
        Console.WriteLine($"Document not found in DB: {id}");
        return NotFound(new { message = $"Document {id} not found" });
    }

    // Normalize the path for Linux/Render
    // Convert Windows backslashes to forward slashes
    var relativePath = document.FilePath.Replace("\\", "/"); 

    // Prepend app root (container root)
    var fullPath = Path.Combine(AppContext.BaseDirectory, relativePath);

    Console.WriteLine($"Looking for PDF at: {fullPath}");

    if (!System.IO.File.Exists(fullPath))
    {
        Console.WriteLine($"PDF not found for document {id}");
        return NotFound(new { message = $"PDF not found for document {id}" });
    }

    var pdfBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
    var fileName = document.Name?.Replace(" ", "_") ?? $"document_{id}.pdf";

    Console.WriteLine($"Returning PDF: {fileName}, Size: {pdfBytes.Length} bytes");

    // PDF.js headers
    Response.Headers.Append("Content-Type", "application/pdf");
    Response.Headers.Append("Content-Disposition", $"inline; filename=\"{fileName}\"");
    Response.Headers.Append("Accept-Ranges", "bytes");
    Response.Headers.Append("Content-Length", pdfBytes.Length.ToString());

    return new FileContentResult(pdfBytes, "application/pdf");
}

/***
    [HttpPost("{id}/sign")]
    public async Task<IActionResult> SignDocument(string id)
    {
        var success = await _documentService.SignDocumentAsync(id);
        if (!success)
            return BadRequest(new { message = "Failed to sign document" });

        var document = await _documentService.GetDocumentByIdAsync(id);
        return Ok(document);
    }
    ***/
    [HttpPost("{id}/sign")]
    public async Task<IActionResult> SignDocument(string id)
    {
        // Let exceptions bubble up so ASP.NET shows the real error
        Console.WriteLine("SIGN ENDPOINT HIT");
        await _documentService.SignDocumentAsync(id);

        var document = await _documentService.GetDocumentByIdAsync(id);
        return Ok(document);
    }

    [HttpGet("unsigned")]
    public async Task<IActionResult> GetUnsignedDocuments([FromQuery] string physicianId)
    {
        if (string.IsNullOrEmpty(physicianId))
        {
            return BadRequest(new { message = "physicianId is required" });
        }

        var documents = await _documentService.GetUnsignedDocumentsByPhysicianIdAsync(physicianId);
        return Ok(documents);
    }

    [HttpGet("signed")]
    public async Task<IActionResult> GetSignedDocuments([FromQuery] string physicianId)
    {
        if (string.IsNullOrEmpty(physicianId))
        {
            return BadRequest(new { message = "physicianId is required" });
        }

        var documents = await _documentService.GetSignedDocumentsByPhysicianIdAsync(physicianId);
        return Ok(documents);
    }

    [HttpGet("counts")]
    public async Task<IActionResult> GetDocumentCounts([FromQuery] string physicianId)
    {
        if (string.IsNullOrEmpty(physicianId))
        {
            return BadRequest(new { message = "physicianId is required" });
        }

        var counts = await _documentService.GetDocumentCountsByPhysicianIdAsync(physicianId);
        return Ok(new { signed = counts.Signed, unsigned = counts.Unsigned });
    }

    [HttpPost("sign-bulk")]
    public async Task<IActionResult> SignDocumentsBulk([FromBody] BulkSignRequest request)
    {
        if (request == null || request.DocumentIds == null || request.DocumentIds.Count == 0)
        {
            return BadRequest(new { message = "DocumentIds array is required" });
        }

        var results = await _documentService.SignDocumentsBulkAsync(request.DocumentIds);
        
        var response = results.Select(r => new
        {
            documentId = r.DocumentId,
            success = r.Success,
            error = r.Error
        }).ToList();

        return Ok(new { results = response });
    }
    

    public class BulkSignRequest
    {
        public List<string> DocumentIds { get; set; } = new List<string>();
    }

}