namespace Spel.Api.Models;

public class GameAction
{
    public int Id { get; set; }

    public int GameId { get; set; }

    public int GameParticipantId { get; set; }

    public GameActionType ActionType { get; set; }

    public int? PlayingCardId { get; set; }

    public int? PawnId { get; set; }

    public int? TargetPawnId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Game? Game { get; set; }

    public GameParticipant? GameParticipant { get; set; }

    public PlayingCard? PlayingCard { get; set; }

    public Pawn? Pawn { get; set; }

    public Pawn? TargetPawn { get; set; }
}