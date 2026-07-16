namespace MyTelegram.Services;

public class AppBrandingOptions
{
    public string Brand { get; set; } = "FamilyGram";
    public string WelcomeMsg { get; set; } = "Welcome to FamilyGram! Your account has been created.";
    public List<string> ProtectedUsernames { get; set; } = [];
}