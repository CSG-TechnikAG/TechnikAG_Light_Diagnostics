namespace TechnikAG_Light_Diagnostics.Models;

public readonly record struct DownloadProgress(long BytesReceived, long? TotalBytes, string FileName)
{
    public double? Percentage
    {
        get
        {
            if (TotalBytes is not long total || total <= 0)
            {
                return null;
            }

            return Math.Clamp(BytesReceived / (double)total * 100.0, 0.0, 100.0);
        }
    }
}
