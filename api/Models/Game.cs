// Validatie en eigenschappen --> [Required], [MaxLength], etc.
using System.ComponentModel.DataAnnotations;

namespace Spel.Api.Models;

public class Game
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    // Standaard status bij nieuw spel
    public GameStatus Status { get; set; } = GameStatus.Waiting;

    [Required]
    public string CreatorId { get; set; } = string.Empty;

    // Nog geen spelers bij nieuw spel
    public string? CurrentPlayerId { get; set; }

    // Standaard ronde bij nieuw spel is 1
    public int Round { get; set; } = 1;

    // Nog geen winnaar bij nieuw spel
    public string? WinnerId { get; set; }

    // Gelijktijdige wijzigingen controleren
    public int Version { get; set; } = 0;

    // Standaard aanmaakdatum bij nieuw spel
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser? Creator { get; set; }

    // Collectiebeheer van deelnemers, kaarten en acties
    public ICollection<GameParticipant> Participants { get; set; }
        = new List<GameParticipant>();

    public ICollection<PlayingCard> Cards { get; set; }
        = new List<PlayingCard>();

    public ICollection<GameAction> Actions { get; set; }
        = new List<GameAction>();
}