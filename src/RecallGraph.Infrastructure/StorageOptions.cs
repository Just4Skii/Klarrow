namespace RecallGraph.Infrastructure;

public sealed class StorageOptions
{
    public string DataRoot { get; set; } = "data";
    public string UploadsPath { get; set; } = "data/uploads";
    public string SeedEvidencePath { get; set; } = "seed/evidence";
    public long MaxUploadBytes { get; set; } = 10 * 1024 * 1024;
}
