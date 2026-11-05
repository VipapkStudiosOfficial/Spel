using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Spel.Api.Data;
using Spel.Api.DTOs;
using Spel.Api.Models;
using Microsoft.EntityFrameworkCore;

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
}