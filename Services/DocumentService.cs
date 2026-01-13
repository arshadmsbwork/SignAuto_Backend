using System.Text.Json;
using iText.IO.Image;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using PhysicianSignatureAPI.Models;

namespace PhysicianSignatureAPI.Services;

public class DocumentService : IDocumentService
{
    private readonly string _contentRootPath;
    private readonly string _storageRoot;
    private readonly string _documentsJsonPath;
    private readonly string _signatureImagePath;
    private readonly IPhysicianService _physicianService;
    
    // Signature placement configuration - Bottom Left
    private const float PAGE_MARGIN_LEFT = 50f;
    private const float PAGE_MARGIN_BOTTOM = 40f;     // Distance from bottom edge to text
    private const float TEXT_SPACING_BELOW_IMAGE = 8f; // Space between text and image
    private const float SIGNATURE_WIDTH = 150f;
    private const float SIGNATURE_HEIGHT = 50f;

    public DocumentService(IWebHostEnvironment env, IPhysicianService physicianService)
    {
        _contentRootPath = env.ContentRootPath;
        _storageRoot = Path.Combine(_contentRootPath, "Storage");
        _documentsJsonPath = Path.Combine(_storageRoot, "documents.json");
        _signatureImagePath = Path.Combine(_storageRoot, "signatures", "signatures.png");
        _physicianService = physicianService;
        
        EnsureStorageExists();
    }

    private void EnsureStorageExists()
    {
        Directory.CreateDirectory(Path.Combine(_storageRoot, "pdfs"));
        Directory.CreateDirectory(Path.Combine(_storageRoot, "signatures"));
        
        if (!File.Exists(_documentsJsonPath))
        {
            var initialDocs = new List<Models.Document>
            {
                new Models.Document
                {
                    Id = "doc-001",
                    Name = "Patient Consent Form",
                    Status = "Pending",
                    FilePath = Path.Combine(_storageRoot, "pdfs", "sample-consent.pdf"),
                    PhysicianId = "phys-001",
                    CreatedAt = DateTime.UtcNow
                },
                new Models.Document
                {
                    Id = "doc-002",
                    Name = "Surgical Consent Form",
                    Status = "Pending",
                    FilePath = Path.Combine(_storageRoot, "pdfs", "sample-consent.pdf"),
                    PhysicianId = "phys-001",
                    CreatedAt = DateTime.UtcNow
                },
                new Models.Document
                {
                    Id = "doc-003",
                    Name = "Medication Authorization",
                    Status = "Pending",
                    FilePath = Path.Combine(_storageRoot, "pdfs", "sample-consent.pdf"),
                    PhysicianId = "phys-002",
                    CreatedAt = DateTime.UtcNow
                }
            };
            
            File.WriteAllText(_documentsJsonPath, JsonSerializer.Serialize(initialDocs, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    public async Task<List<Models.Document>> GetAllDocumentsAsync()
    {
        var json = await File.ReadAllTextAsync(_documentsJsonPath);
        return JsonSerializer.Deserialize<List<Models.Document>>(json) ?? new List<Models.Document>();
    }

    public async Task<Models.Document?> GetDocumentByIdAsync(string id)
    {
        var docs = await GetAllDocumentsAsync();
        return docs.FirstOrDefault(d => d.Id == id);
    }

    private string ResolveFilePath(string filePath)
    {
        // Normalize path separators
        filePath = filePath.Replace('/', Path.DirectorySeparatorChar);
        
        // If path is already absolute, return as is
        if (Path.IsPathRooted(filePath))
            return Path.GetFullPath(filePath);
        
        // If path starts with "Storage", resolve relative to content root
        if (filePath.StartsWith("Storage", StringComparison.OrdinalIgnoreCase))
        {
            // Remove leading "Storage" and any path separators
            var relativePath = filePath.Substring("Storage".Length).TrimStart(Path.DirectorySeparatorChar, '/', '\\');
            return Path.GetFullPath(Path.Combine(_contentRootPath, "Storage", relativePath));
        }
        
        // Otherwise, resolve relative to storage root
        return Path.GetFullPath(Path.Combine(_storageRoot, filePath));
    }

    public async Task<byte[]?> GetPdfBytesAsync(string id)
    {
        var doc = await GetDocumentByIdAsync(id);
        if (doc == null)
        {
            Console.WriteLine($"Document not found: {id}");
            return null;
        }

        var filePath = doc.Status == "Signed" && !string.IsNullOrEmpty(doc.SignedFilePath) 
            ? doc.SignedFilePath 
            : doc.FilePath;

        Console.WriteLine($"Original file path: {filePath}");

        // Resolve relative paths to absolute paths
        var resolvedPath = ResolveFilePath(filePath);
        Console.WriteLine($"Resolved file path: {resolvedPath}");

        if (!File.Exists(resolvedPath))
        {
            Console.WriteLine($"PDF file not found at path: {resolvedPath}");
            Console.WriteLine($"Directory exists: {Directory.Exists(Path.GetDirectoryName(resolvedPath))}");
            return null;
        }

        try
        {
            var bytes = await File.ReadAllBytesAsync(resolvedPath);
            Console.WriteLine($"Successfully read PDF file: {bytes.Length} bytes");
            return bytes;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading PDF file: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return null;
        }
    }

    public async Task<bool> SignDocumentAsync(string id)
    {
        var doc = await GetDocumentByIdAsync(id);
        if (doc == null) return false;
        
        var resolvedFilePath = ResolveFilePath(doc.FilePath);
        if (!File.Exists(resolvedFilePath)) return false;

        try
        {
            // Get physician name for signature
            string physicianName = "Physician";
            if (!string.IsNullOrEmpty(doc.PhysicianId))
            {
                var physician = await _physicianService.GetPhysicianByIdAsync(doc.PhysicianId);
                if (physician != null)
                {
                    physicianName = physician.Name;
                }
            }

            // Generate signed file path
            var fileName = Path.GetFileNameWithoutExtension(resolvedFilePath);
            var signedFileName = $"{fileName}_signed.pdf";
            var signedFilePath = Path.Combine(Path.GetDirectoryName(resolvedFilePath)!, signedFileName);

            // Apply signature to PDF with physician name
            await ApplySignatureToPdf(resolvedFilePath, signedFilePath, physicianName);

            // Update document status
            doc.Status = "Signed";
            doc.SignedFilePath = signedFilePath;
            doc.SignedAt = DateTime.UtcNow;

            await UpdateDocumentAsync(doc);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("===== SIGN DOCUMENT ERROR =====");
            Console.WriteLine(ex.ToString());   // FULL stack trace
            Console.WriteLine("================================");
            throw; // <-- TEMPORARY: lets ASP.NET show the real error
        }
    }

    private async Task ApplySignatureToPdf(string sourcePath, string destPath, string physicianName)
    {
        Console.WriteLine("=== APPLY SIGNATURE START ===");
        Console.WriteLine($"Source PDF: {sourcePath}");
        Console.WriteLine($"Destination PDF: {destPath}");
        Console.WriteLine($"Signature image: {_signatureImagePath}");
        Console.WriteLine($"Physician Name: {physicianName}");

        if (!File.Exists(_signatureImagePath))
            throw new FileNotFoundException("Signature image not found", _signatureImagePath);

        using var reader = new PdfReader(sourcePath);
        using var writer = new PdfWriter(destPath);
        using var pdfDoc = new PdfDocument(reader, writer);

        Console.WriteLine($"Pages found: {pdfDoc.GetNumberOfPages()}");

        using var document = new iText.Layout.Document(pdfDoc);

        var imageData = ImageDataFactory.Create(_signatureImagePath);
        var signDate = DateTime.Now.ToString("MM/dd/yyyy");
        var signatureText = $"Electronically Signed by {physicianName} on {signDate}";

        for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
        {
            Console.WriteLine($"Stamping page {i}");

            var pageSize = pdfDoc.GetPage(i).GetPageSize();

            // Start the entire block from the bottom
            float currentY = PAGE_MARGIN_BOTTOM;  // This will be the bottom of the text

            // First: Add the text (lowest element)
            document.Add(
                new Paragraph(signatureText)
                    .SetFontSize(10)
                    .SetFixedPosition(i, PAGE_MARGIN_LEFT, currentY, pageSize.GetWidth() - PAGE_MARGIN_LEFT - 40f)
                    .SetMarginTop(0)
                    .SetMarginBottom(0)
            );

            // Estimate text height (10pt font ≈ 12pt line height)
            float textHeight = 14f;

            // Move up for spacing + text height
            currentY += textHeight + TEXT_SPACING_BELOW_IMAGE;

            // Now add the signature image above the text
            var signatureImage = new Image(imageData)
                .SetWidth(SIGNATURE_WIDTH)
                .SetHeight(SIGNATURE_HEIGHT)
                .SetFixedPosition(i, PAGE_MARGIN_LEFT, currentY);

            document.Add(signatureImage);

            // Optional: ensure we don't go too high — but usually not needed
        }

        Console.WriteLine("=== APPLY SIGNATURE COMPLETE ===");
        await Task.CompletedTask;
    }

    private async Task UpdateDocumentAsync(Models.Document updatedDoc)
    {
        var docs = await GetAllDocumentsAsync();
        var index = docs.FindIndex(d => d.Id == updatedDoc.Id);
        
        if (index >= 0)
        {
            docs[index] = updatedDoc;
            var json = JsonSerializer.Serialize(docs, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_documentsJsonPath, json);
        }
    }

    public async Task<List<Models.Document>> GetDocumentsByPhysicianIdAsync(string physicianId)
    {
        var allDocs = await GetAllDocumentsAsync();
        return allDocs.Where(d => d.PhysicianId == physicianId).ToList();
    }

    public async Task<List<Models.Document>> GetUnsignedDocumentsByPhysicianIdAsync(string physicianId)
    {
        var allDocs = await GetAllDocumentsAsync();
        return allDocs.Where(d => d.PhysicianId == physicianId && d.Status == "Pending").ToList();
    }

    public async Task<List<Models.Document>> GetSignedDocumentsByPhysicianIdAsync(string physicianId)
    {
        var allDocs = await GetAllDocumentsAsync();
        return allDocs.Where(d => d.PhysicianId == physicianId && d.Status == "Signed").ToList();
    }

    public async Task<(int Signed, int Unsigned)> GetDocumentCountsByPhysicianIdAsync(string physicianId)
    {
        var allDocs = await GetAllDocumentsAsync();
        var physicianDocs = allDocs.Where(d => d.PhysicianId == physicianId).ToList();
        
        var signed = physicianDocs.Count(d => d.Status == "Signed");
        var unsigned = physicianDocs.Count(d => d.Status == "Pending");
        
        return (signed, unsigned);
    }

    public async Task<List<(string DocumentId, bool Success, string? Error)>> SignDocumentsBulkAsync(List<string> documentIds)
    {
        var results = new List<(string DocumentId, bool Success, string? Error)>();

        foreach (var documentId in documentIds)
        {
            try
            {
                var success = await SignDocumentAsync(documentId);
                results.Add((documentId, success, success ? null : "Failed to sign document"));
            }
            catch (Exception ex)
            {
                results.Add((documentId, false, ex.Message));
            }
        }

        return results;
    }
}