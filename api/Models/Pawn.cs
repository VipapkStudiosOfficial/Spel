using System.ComponentModel.DataAnnotations;

namespace Spel.Api.Models;

public class Pawn
{
    public int Id { get; set; }

    public int GameParticipantId { get; set; }

    // Nog geen pion op positie bij nieuw spel
    public int? Position { get; set; }

    // Progress van pion in het spel
    public int Progress { get; set; } = 0;

    public bool IsFinished { get; set; } = false;

    public GameParticipant? GameParticipant { get; set; }
}