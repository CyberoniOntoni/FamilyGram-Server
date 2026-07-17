namespace MyTelegram.FileServer.Options;

public class MinioOptions
{
    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public string BucketName { get; set; } = "tg-files";
    public bool CreateBucketIfNotExists { get; set; } = true;
    public bool UseSsl { get; set; }
}

public class FileServerAppOptions
{
    public string DatabaseName { get; set; } = "tg";
    public int ThisDcId { get; set; } = 1;
    public int MediaDcId { get; set; } = 1;
}
