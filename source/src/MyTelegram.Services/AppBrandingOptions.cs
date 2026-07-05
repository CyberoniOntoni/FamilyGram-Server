namespace MyTelegram.Services;

public class AppBrandingOptions
{
    public string Brand { get; set; } = "Testgram";
    public string WelcomeMsg { get; set; } = "Welcome to Testgram! Your account has been created.";
    public List<string> ProtectedUsernames { get; set; } = [];
}