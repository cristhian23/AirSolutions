namespace AirSolutions.Models.Auth;

public class JwtSettings
{
    public string Issuer { get; set; } = "AirSolutions";
    public string Audience { get; set; } = "AirSolutions.Web";
    public string Key { get; set; } = "CHANGE_THIS_DEV_KEY_WITH_32_PLUS_CHARACTERS_123456";
    public int ExpiresMinutes { get; set; } = 480;
}
