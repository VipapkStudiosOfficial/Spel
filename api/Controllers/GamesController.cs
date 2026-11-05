using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Spel.Api.Data;
using Spel.Api.DTOs;
using Spel.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Spel.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GamesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public GamesController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpPost]
    public async Task<IActionResult> CreateGame(CreateGameDto dto)
    {
        var userId = _userManager.GetUserId(User);

        if (userId == null)
        {
            return Unauthorized();
        }

    var game = new Game
    {
        Name = dto.Name,
        CreatorId = userId
    };

    var participant = new GameParticipant
    {
        UserId = userId,
        Color = PlayerColor.Red,
        SeatOrder = 0
    };

    for (int i = 0; i < 4; i++)
    {
        participant.Pawns.Add(new Pawn());
    }

    game.Participants.Add(participant);
        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        return Created($"/api/games/{game.Id}", new
        {
            game.Id,
            game.Name,
            game.Status,
            game.CreatedAt
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetGames()
    {
        var games = await _context.Games
            .Where(g => g.Status == GameStatus.Waiting)
            .Select(g => new GameListDto
            {
                Id = g.Id,
                Name = g.Name,
                CreatedAt = g.CreatedAt,
                PlayerCount = g.Participants.Count()
            })
            .ToListAsync();

        return Ok(games);
    }

    [HttpPost("{id}/join")]
    public async Task<IActionResult> JoinGame(int id)
    {
        // Zoek het spel op
        var game = await _context.Games
            .Include(g => g.Participants)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (game == null)
        {
            return NotFound("Spel niet gevonden.");
        }

        // Controleer of het spel nog wacht
        if (game.Status != GameStatus.Waiting)
        {
            return BadRequest("Dit spel is al gestart.");
        }

        // Haal de ingelogde gebruiker op
        var userId = _userManager.GetUserId(User);

        if (userId == null)
        {
            return Unauthorized();
        }

        // Controleer of de speler al deelneemt
        if (game.Participants.Any(p => p.UserId == userId))
        {
            return BadRequest("Je neemt al deel aan dit spel.");
        }

        // Controleer of het spel vol is
        if (game.Participants.Count >= 4)
        {
            return BadRequest("Dit spel zit al vol.");
        }

        // Bepaal de zitplaats
        int seatOrder = game.Participants.Count;

        // Maak de nieuwe speler aan
        var participant = new GameParticipant
        {
            GameId = game.Id,
            UserId = userId,
            SeatOrder = seatOrder,
            Color = (PlayerColor)seatOrder
        };

        // Geef de speler vier pionnen
        for (int i = 0; i < 4; i++)
        {
            participant.Pawns.Add(new Pawn());
        }

        _context.GameParticipants.Add(participant);

        await _context.SaveChangesAsync();

        return Ok("Je bent succesvol toegevoegd aan het spel!");
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetGameDetails(int id)
    {
        var game = await _context.Games
            .Where(g => g.Id == id)
            .Select(g => new GameDetailsDto
            {
                Id = g.Id,
                Name = g.Name,
                Status = g.Status,
                PlayerCount = g.Participants.Count(),
                Players = g.Participants
                    .OrderBy(p => p.SeatOrder)
                    .Select(p => p.User!.Email!)
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (game == null)
        {
            return NotFound("Spel niet gevonden.");
        }

        return Ok(game);
    }

    [HttpPost("{id}/start")]
    public async Task<IActionResult> StartGame(int id)
    {
        var userId = _userManager.GetUserId(User);

        if (userId == null)
        {
            return Unauthorized();
        }

        // Zoek het spel en de deelnemers
        var game = await _context.Games
            .Include(g => g.Participants)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (game == null)
        {
            return NotFound("Spel niet gevonden.");
        }

        // Alleen de maker mag het spel starten
        if (game.CreatorId != userId)
        {
            return Forbid();
        }

        if (game.Status != GameStatus.Waiting)
        {
            return BadRequest("Dit spel is al gestart.");
        }

        var players = game.Participants
            .OrderBy(p => p.SeatOrder)
            .ToList();

        if (players.Count < 2 || players.Count > 4)
        {
            return BadRequest("Er moeten 2 tot 4 spelers deelnemen.");
        }

        // Maak een kaartspel van 52 kaarten
        var deck = new List<PlayingCard>();

        foreach (CardSuit suit in Enum.GetValues<CardSuit>())
        {
            foreach (CardValue value in Enum.GetValues<CardValue>())
            {
                deck.Add(new PlayingCard
                {
                    GameId = game.Id,
                    Suit = suit,
                    Value = value
                });
            }
        }

        // Schud de kaarten
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }

        // Deel 10 kaarten uit aan iedere speler
        int cardIndex = 0;

        foreach (var player in players)
        {
            for (int i = 0; i < 10; i++)
            {
                deck[cardIndex].OwnerId = player.Id;
                cardIndex++;
            }
        }

        // Start het spel
        game.Status = GameStatus.Active;
        game.CurrentPlayerId = players[0].Id;
        game.Round = 1;
        game.Version++;

        _context.PlayingCards.AddRange(deck);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            game.Id,
            game.Name,
            game.Status,
            game.Round,
            PlayerCount = players.Count,
            CardsPerPlayer = 10,
            RemainingCards = deck.Count - cardIndex
        });
    }

    [HttpGet("{id}/hand")]
    public async Task<IActionResult> GetHand(int id)
    {
        var userId = _userManager.GetUserId(User);

        if (userId == null)
        {
            return Unauthorized();
        }

        var game = await _context.Games
            .FirstOrDefaultAsync(g => g.Id == id);

        if (game == null)
        {
            return NotFound("Spel niet gevonden.");
        }

        if (game.Status != GameStatus.Active)
        {
            return BadRequest("Dit spel is nog niet actief.");
        }

        var participant = await _context.GameParticipants
            .Include(p => p.Cards)
            .FirstOrDefaultAsync(p =>
                p.GameId == id && p.UserId == userId);

        if (participant == null)
        {
            return Forbid();
        }

        var hand = participant.Cards
            .Where(c => !c.IsPlayed)
            .Select(c => new
            {
                c.Id,
                c.Suit,
                c.Value
            })
            .ToList();

        return Ok(hand);
    }
}