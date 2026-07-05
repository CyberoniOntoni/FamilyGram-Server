namespace MyTelegram.DataSeeder;

public class MyTelegramDataSeederOptions
{
    public string Brand { get; set; } = "Testgram";
    public List<string> ProtectedUsernames { get; set; } = [];
    public bool UploadNewDocumentFiles { get; set; }
    public MyTelegramBotOptions MyTelegramBotOptions { get; set; } = null!;
    public bool CreateTestUsers { get; set; }
}