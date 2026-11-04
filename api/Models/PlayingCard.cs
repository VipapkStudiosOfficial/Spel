using System.ComponentModel.DataAnnotations;

namespace Spel.Api.Models;

public class PlayingCard
{
    public int Id { get; set; }

    public CardSuit Suit { get; set; }

    public CardValue Value { get; set; }

    // Nog geen eigenaar bij nieuw spel
    public int? OwnerId { get; set; }

    public int GameId { get; set; }

    public bool IsPlayed { get; set; } = false;

    public Game? Game { get; set; }

    public GameParticipant? Owner { get; set; }
}