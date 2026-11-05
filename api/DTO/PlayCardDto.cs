namespace Spel.Api.DTO;

public class PlayCardDto
{
    public int PlayingCardId { get; set; }
    public int PawnId { get; set; }

    public bool DeployPawn { get; set; }

    public int? TargetPawnId { get; set; }

    public List<SevenMoveDto> SevenMoves { get; set; } = new();

    public int? OpponentParticipantId { get; set; }
    public int? ExchangeCardId { get; set; }
    public int? OpponentCardId { get; set; }
}