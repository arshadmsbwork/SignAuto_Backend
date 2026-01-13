using PhysicianSignatureAPI.Models;

namespace PhysicianSignatureAPI.Services;

public interface IDocumentService
{
    Task<List<Models.Document>> GetAllDocumentsAsync();
    Task<Models.Document?> GetDocumentByIdAsync(string id);
    Task<byte[]?> GetPdfBytesAsync(string id);
    Task<bool> SignDocumentAsync(string id);
    Task<List<Models.Document>> GetDocumentsByPhysicianIdAsync(string physicianId);
    Task<List<Models.Document>> GetUnsignedDocumentsByPhysicianIdAsync(string physicianId);
    Task<List<Models.Document>> GetSignedDocumentsByPhysicianIdAsync(string physicianId);
    Task<(int Signed, int Unsigned)> GetDocumentCountsByPhysicianIdAsync(string physicianId);
    Task<List<(string DocumentId, bool Success, string? Error)>> SignDocumentsBulkAsync(List<string> documentIds);
}