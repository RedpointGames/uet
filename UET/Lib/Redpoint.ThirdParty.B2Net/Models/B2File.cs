namespace B2Net.Models
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    public class B2File
    {
        public string FileId { get; set; }
        public string FileName { get; set; }
        public string Action { get; set; }
        public long Size { get; set; }
        public long? UploadTimestamp { get; set; }
        public Stream FileData { get; set; }
        // Uploaded File Response
        public long? ContentLength { get; set; }
        public string ContentSHA1 { get; set; }
        public string ContentType { get; set; }
        public Dictionary<string, string> FileInfo { get; set; }
        // End

        public DateTime UploadTimestampDate
        {
            get
            {
                if (!UploadTimestamp.HasValue)
                {
                    return DateTimeOffset.FromUnixTimeSeconds(UploadTimestamp.Value).UtcDateTime;
                }
                else
                {
                    return DateTime.Now;
                }
            }
        }
    }
}
