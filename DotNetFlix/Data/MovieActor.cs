namespace DotNetFlix.Data;

internal class MovieActor
{
    public long Id { get; set; }
    public long MovieId { get; set; }
    public long ActorId { get; set; }
    public string Role { get; set; }
}
