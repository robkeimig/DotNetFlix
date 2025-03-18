using Microsoft.Data.Sqlite;

namespace DotNetFlix.Data;

public class Movie
{
    public long Id { get; set; }
    public string Title { get; set; }
    public int Year { get; set; }
    public int DurationMinutes { get; set; }
    public string Genre { get; set; }
    public long? DirectorId { get; set; }
    public long? WriterId { get; set; }
    public string Language { get; set; }
    public long? PosterMediaId { get; set; }   
    public long? TrailerMediaId { get; set; }
    public long? FeatureMediaId { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public class MoviesTable
{
    public long Id { get; set; }
    public string Title { get; set; }
    public int Year { get; set; }
    public int DurationMinutes { get; set; }
    public string Genre { get; set; }
    public long? DirectorId { get; set; }
    public long? WriterId { get; set; }
    public string Language { get; set; }
    public long? PosterMediaId { get; set; }
    public long? TrailerMediaId { get; set; }
    public long? FeatureMediaId { get; set; }
    public long CreatedUnixTimestamp { get; set; }
}

public static class MovieDataExtensions
{
    public static List<Movie> GetMovies(this SqliteConnection sql, long sessionId)
    {
        return new List<Movie>()
        {
            new Movie
            {
                CreatedUtc = DateTime.Today,
                DurationMinutes = 120,
                Language = "English",
                Genre = "Adventure",
                Year = 1999,
                Title = "You know its a good one",
            }
        };  
    }
}