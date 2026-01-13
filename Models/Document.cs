namespace PhysicianSignatureAPI.Models;

public class Document
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending or Signed
    public string FilePath { get; set; } = string.Empty;
    public string? SignedFilePath { get; set; }
    public string PhysicianId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SignedAt { get; set; }
}