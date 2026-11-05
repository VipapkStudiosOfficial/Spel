using Spel.Api.Models;
namespace Spel.Api.DTO;

public class GameDetailsDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public GameStatus Status { get; set; }
    public int PlayerCount { get; set; }
    public List<string> Players { get; set; } = new();
}