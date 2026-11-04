using System.ComponentModel.DataAnnotations;

namespace Spel.Api.Models;

public class GameParticipant
{
    public int Id { get; set; }

    public int GameId { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public PlayerColor Color { get; set; }

    public int SeatOrder { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Game? Game { get; set; }

    public ApplicationUser? User { get; set; }

    // Collectiebeheer van pionnen, kaarten en acties
    public ICollection<Pawn> Pawns { get; set; }
        = new List<Pawn>();

    public ICollection<PlayingCard> Cards { get; set; }
        = new List<PlayingCard>();

    public ICollection<GameAction> Actions { get; set; }
        = new List<GameAction>();
}