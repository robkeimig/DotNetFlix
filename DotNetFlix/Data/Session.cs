namespace DotNetFlix.Data;

public class Session
{
    public long Id;
    public string Token;
    public string Resource;
    public DateTime CreatedUtc;
    public DateTime LastUsedUtc;
}
