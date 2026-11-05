namespace Spel.Api.DTOs;

public class GameListDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int PlayerCount { get; set; }
}