using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Spel.Api.Data;
using Spel.Api.DTO;
using Spel.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using Spel.Api.Services;

namespace Spel.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GamesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

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

    [HttpPost("{id}/play")]
    public async Task<IActionResult> PlayCard(int id, PlayCardDto dto)
    {
        var userId = _userManager.GetUserId(User);

        if (userId == null)
        {
            return Unauthorized();
        }

        // Zoek het spel
        var game = await _context.Games
            .Include(g => g.Participants)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (game == null)
        {
            return NotFound("Spel niet gevonden.");
        }

        if (game.Status != GameStatus.Active)
        {
            return BadRequest("Dit spel is niet actief.");
        }

        // Zoek de speler
        var participant = game.Participants
            .FirstOrDefault(p => p.UserId == userId);

        if (participant == null)
        {
            return Forbid();
        }

        // Controleer of de speler aan de beurt is
        if (game.CurrentPlayerId != participant.Id)
        {
            return BadRequest("Je bent niet aan de beurt.");
        }

        // Zoek de kaart
        var card = await _context.PlayingCards
            .FirstOrDefaultAsync(c =>
                c.Id == dto.PlayingCardId &&
                c.GameId == id &&
                c.OwnerId == participant.Id &&
                !c.IsPlayed);

        if (card == null)
        {
            return BadRequest("Deze kaart is niet van jou of is al gespeeld.");
        }

        // Zoek de pion
        var pawn = await _context.Pawns
            .FirstOrDefaultAsync(p =>
                p.Id == dto.PawnId &&
                p.GameParticipantId == participant.Id);

        if (pawn == null)
        {
            return BadRequest("Deze pion is niet van jou.");
        }

        if (card.Value == CardValue.Four ||
            card.Value == CardValue.Ace)
        {
            return BadRequest("Deze speciale kaart wordt nog niet ondersteund.");
        }

        // Voor nu ondersteunen we alleen gewone cijferkaarten
        // J, Q en K verplaatsen een pion 10 stappen
        int movement = (card.Value == CardValue.Jack ||
                        card.Value == CardValue.Queen ||
                        card.Value == CardValue.King)
            ? 10
            : (int)card.Value;

        // J, Q en K kunnen een pion inzetten
        if (card.Value == CardValue.Jack ||
            card.Value == CardValue.Queen ||
            card.Value == CardValue.King)
        {
            if (dto.DeployPawn)
            {
                // Controleer of de pion al op het bord staat
                if (pawn.Position != null)
                {
                    return BadRequest("Deze pion staat al op het bord.");
                }

                // Bepaal het startvak van de speler
                int deployPosition = participant.SeatOrder * 10;

                // Controleer of het startvak vrij is
                bool startOccupied = await _context.Pawns.AnyAsync(p =>
                    p.Position == deployPosition);

                if (startOccupied)
                {
                    return BadRequest("Je startvak is bezet.");
                }

                // Zet de pion op het bord
                pawn.Position = deployPosition;
                pawn.Progress = 0;

                card.IsPlayed = true;

                _context.GameActions.Add(new GameAction
                {
                    GameId = game.Id,
                    GameParticipantId = participant.Id,
                    ActionType = GameActionType.DeployPawn,
                    PlayingCardId = card.Id,
                    PawnId = pawn.Id
                });

                // Geef de beurt door
                var deployPlayers = game.Participants
                    .OrderBy(p => p.SeatOrder)
                    .ToList();

                int deployCurrentIndex = deployPlayers.FindIndex(
                    p => p.Id == participant.Id);

                int deployNextIndex = (deployCurrentIndex + 1) % deployPlayers.Count;

                game.CurrentPlayerId = deployPlayers[deployNextIndex].Id;
                game.Version++;

                await _context.SaveChangesAsync();

                return Ok("Je pion staat nu op het startvak.");
            }
        }

        // Een pion moet eerst op het bord staan
        if (pawn.Position == null)
        {
            return BadRequest("Deze pion staat nog in de basis.");
        }

        // Een 8 wisselt jouw pion met een pion van een tegenstander
        if (card.Value == CardValue.Eight)
        {
            if (dto.TargetPawnId == null)
            {
                return BadRequest("Selecteer een pion van een tegenstander.");
            }

            // Zoek de pion van de tegenstander
            var swapTargetPawn = await _context.Pawns
                .Include(p => p.GameParticipant)
                .FirstOrDefaultAsync(p =>
                    p.Id == dto.TargetPawnId &&
                    p.GameParticipant!.GameId == id &&
                    p.GameParticipantId != participant.Id &&
                    p.Position != null);

            if (swapTargetPawn == null)
            {
                return BadRequest("Ongeldige pion van de tegenstander.");
            }

            // Controleer of de tegenstander op zijn eigen startvak staat
            int opponentStart = swapTargetPawn.GameParticipant!.SeatOrder * 10;

            if (swapTargetPawn.Position == opponentStart &&
                swapTargetPawn.Progress == 0)
            {
                return BadRequest("Je kunt geen pion op een beschermd startvak wisselen.");
            }

            // Bewaar de posities
            if (swapTargetPawn.Position == null || pawn.Position == null)
            {
                return BadRequest("Een van de pionnen staat niet op het bord.");
            }

            int ownOldPosition = pawn.Position.Value;
            int opponentOldPosition = swapTargetPawn.Position.Value;

            // Wissel de posities en bereken de voortgang opnieuw
            int ownStart = participant.SeatOrder * 10;

            pawn.Position = opponentOldPosition;
            pawn.Progress = (opponentOldPosition - ownStart + 40) % 40;

            swapTargetPawn.Position = ownOldPosition;

            int opponentNewStart = swapTargetPawn.GameParticipant.SeatOrder * 10;

            swapTargetPawn.Progress =
                (ownOldPosition - opponentNewStart + 40) % 40;

            // Verbruik de kaart
            card.IsPlayed = true;

            // Registreer de actie
            _context.GameActions.Add(new GameAction
            {
                GameId = game.Id,
                GameParticipantId = participant.Id,
                ActionType = GameActionType.SwapPawns,
                PlayingCardId = card.Id,
                PawnId = pawn.Id,
                TargetPawnId = swapTargetPawn.Id
            });

            // Geef de beurt door
            var swapPlayers = game.Participants
                .OrderBy(p => p.SeatOrder)
                .ToList();

            int swapCurrentIndex = swapPlayers.FindIndex(
                p => p.Id == participant.Id);

            int swapNextIndex = (swapCurrentIndex + 1) % swapPlayers.Count;

            game.CurrentPlayerId = swapPlayers[swapNextIndex].Id;
            game.Version++;

            await _context.SaveChangesAsync();

            return Ok("Je hebt de pionnen verwisseld.");
        }

        // Een 7 mag verdeeld worden over meerdere eigen pionnen
        if (card.Value == CardValue.Seven)
        {
            if (dto.SevenMoves == null || dto.SevenMoves.Count == 0)
                return BadRequest("Geef minimaal één pionbeweging op.");

            if (dto.SevenMoves.Any(m => m.Steps <= 0))
                return BadRequest("Elke beweging moet minimaal één stap zijn.");

            if (dto.SevenMoves.Sum(m => m.Steps) != 7)
                return BadRequest("De bewegingen moeten samen precies 7 stappen zijn.");

            var sevenPawnIds = dto.SevenMoves.Select(m => m.PawnId).ToList();

            if (sevenPawnIds.Distinct().Count() != sevenPawnIds.Count)
                return BadRequest("Je mag dezelfde pion niet meerdere keren selecteren.");

            var sevenPawns = await _context.Pawns
                .Where(p => sevenPawnIds.Contains(p.Id) &&
                            p.GameParticipantId == participant.Id)
                .ToListAsync();

            if (sevenPawns.Count != sevenPawnIds.Count ||
                sevenPawns.Any(p => p.Position == null))
                return BadRequest("Een geselecteerde pion staat niet op het bord.");

            // Verwerk de bewegingen
            foreach (var move in dto.SevenMoves)
            {
                var selectedPawn = sevenPawns.First(p => p.Id == move.PawnId);

                int sevenNewProgress = selectedPawn.Progress + move.Steps;

                if (sevenNewProgress > 40)
                    return BadRequest("Een pion mag niet verder dan de finish.");

                int sevenStartPosition = participant.SeatOrder * 10;
                int sevenNewPosition = (sevenStartPosition + sevenNewProgress) % 40;

                // Controleer de volledige route van deze pion
                for (int step = 1; step <= move.Steps; step++)
                {
                    int sevenCheckPosition =
                        ((sevenStartPosition + selectedPawn.Progress + step) % 40 + 40) % 40;

                    bool sevenBlockingPawn = await _context.Pawns
                        .Include(p => p.GameParticipant)
                        .AnyAsync(p =>
                            p.GameParticipant!.GameId == id &&
                            p.Id != selectedPawn.Id &&
                            p.Position == sevenCheckPosition &&
                            (
                                (p.GameParticipantId == participant.Id &&
                                p.Progress == 0 &&
                                p.Position == sevenStartPosition)
                                ||
                                (p.Progress == 0 &&
                                p.Position == p.GameParticipant.SeatOrder * 10)
                            ));

                    if (sevenBlockingPawn)
                    {
                        return BadRequest("Een startpion blokkeert de doorgang.");
                    }
                }

                if (sevenNewProgress == 40)
                {
                    selectedPawn.Progress = 40;
                    selectedPawn.Position = null;
                    selectedPawn.IsFinished = true;
                }
                else
                {
                    bool occupiedByOwnPawn = sevenPawns.Any(p =>
                        p.Id != selectedPawn.Id && p.Position == sevenNewPosition);

                    if (occupiedByOwnPawn)
                        return BadRequest("Er staat al een eigen pion op die positie.");

                    // Zoek een tegenstander op de bestemming
                    var sevenOpponentPawn = await _context.Pawns
                        .FirstOrDefaultAsync(p =>
                            p.GameParticipant!.GameId == id &&
                            p.GameParticipantId != participant.Id &&
                            p.Position == sevenNewPosition);

                    if (sevenOpponentPawn != null)
                    {
                        // Stuur de tegenstander terug naar de basis
                        sevenOpponentPawn.Position = null;
                        sevenOpponentPawn.Progress = 0;
                    }

                    // Verplaats de eigen pion
                    selectedPawn.Progress = sevenNewProgress;
                    selectedPawn.Position = sevenNewPosition;
                }
            }

            bool sevenAllFinished = await _context.Pawns
                .Where(p => p.GameParticipantId == participant.Id)
                .AllAsync(p => p.IsFinished);

            if (sevenAllFinished)
            {
                game.Status = GameStatus.Finished;
                game.WinnerId = participant.Id;
                game.CurrentPlayerId = null;
            }

            card.IsPlayed = true;

            _context.GameActions.Add(new GameAction
            {
                GameId = game.Id,
                GameParticipantId = participant.Id,
                ActionType = GameActionType.MovePawn,
                PlayingCardId = card.Id
            });

            var sevenPlayers = game.Participants
                .OrderBy(p => p.SeatOrder)
                .ToList();

            int sevenCurrentIndex = sevenPlayers.FindIndex(
                p => p.Id == participant.Id);

            game.CurrentPlayerId =
                sevenPlayers[(sevenCurrentIndex + 1) % sevenPlayers.Count].Id;

            game.Version++;

            await _context.SaveChangesAsync();

            return Ok("Je hebt alle zeven stappen verdeeld.");
        }

        // Een Aas laat je een kaart ruilen met een tegenstander
        if (card.Value == CardValue.Ace)
        {
            if (dto.OpponentParticipantId == null ||
                dto.ExchangeCardId == null ||
                dto.OpponentCardId == null)
            {
                return BadRequest("Vul alle gegevens voor de kaartwissel in.");
            }

            // Zoek de tegenstander in hetzelfde spel
            var aceOpponent = game.Participants.FirstOrDefault(p =>
                p.Id == dto.OpponentParticipantId &&
                p.Id != participant.Id);

            if (aceOpponent == null)
                return BadRequest("Ongeldige tegenstander.");

            // Zoek jouw kaart die je wilt ruilen
            var ownExchangeCard = await _context.PlayingCards
                .FirstOrDefaultAsync(c =>
                    c.Id == dto.ExchangeCardId &&
                    c.GameId == id &&
                    c.OwnerId == participant.Id &&
                    !c.IsPlayed &&
                    c.Id != card.Id);

            if (ownExchangeCard == null)
                return BadRequest("Je eigen ruilkaart is ongeldig.");

            // Zoek de kaart van de tegenstander
            var opponentExchangeCard = await _context.PlayingCards
                .FirstOrDefaultAsync(c =>
                    c.Id == dto.OpponentCardId &&
                    c.GameId == id &&
                    c.OwnerId == aceOpponent.Id &&
                    !c.IsPlayed);

            if (opponentExchangeCard == null)
                return BadRequest("De kaart van de tegenstander is ongeldig.");

            // Controleer of beide spelers na de actie kaarten overhouden
            int ownHandCount = await _context.PlayingCards.CountAsync(c =>
                c.OwnerId == participant.Id && !c.IsPlayed);

            int opponentHandCount = await _context.PlayingCards.CountAsync(c =>
                c.OwnerId == aceOpponent.Id && !c.IsPlayed);

            if (ownHandCount - 1 < 1 || opponentHandCount < 1)
                return BadRequest("Beide spelers moeten minimaal één kaart overhouden.");

            // Wissel de kaarten
            ownExchangeCard.OwnerId = aceOpponent.Id;
            opponentExchangeCard.OwnerId = participant.Id;

            // Verbruik de Aas
            card.IsPlayed = true;

            // Registreer de actie
            _context.GameActions.Add(new GameAction
            {
                GameId = game.Id,
                GameParticipantId = participant.Id,
                ActionType = GameActionType.PlayCard,
                PlayingCardId = card.Id
            });

            // Geef de beurt door
            var acePlayers = game.Participants
                .OrderBy(p => p.SeatOrder)
                .ToList();

            int aceCurrentIndex = acePlayers.FindIndex(
                p => p.Id == participant.Id);

            game.CurrentPlayerId =
                acePlayers[(aceCurrentIndex + 1) % acePlayers.Count].Id;

            game.Version++;

            await _context.SaveChangesAsync();

            return Ok("Je hebt de Aas gespeeld en de kaarten geruild.");
        }

        // Een 4 verplaatst de pion vier stappen achteruit
        if (card.Value == CardValue.Four)
        {
            int backwardProgress = pawn.Progress - 4;

            int backwardStart = participant.SeatOrder * 10;
            int backwardPosition = ((backwardStart + backwardProgress) % 40 + 40) % 40;

            // Controleer of een beschermde startpion de route blokkeert
            for (int step = 1; step <= 4; step++)
            {
                int checkPosition =
                    ((backwardStart + pawn.Progress - step) % 40 + 40) % 40;

                bool blockingPawn = await _context.Pawns
                    .Include(p => p.GameParticipant)
                    .AnyAsync(p =>
                        p.GameParticipant!.GameId == id &&
                        p.Id != pawn.Id &&
                        p.Position == checkPosition &&
                        p.Progress == 0 &&
                        p.Position == p.GameParticipant.SeatOrder * 10);

                if (blockingPawn)
                {
                    return BadRequest("Een beschermde startpion blokkeert de doorgang.");
                }
            }

            bool backwardDestinationOccupied = await _context.Pawns.AnyAsync(p =>
                p.GameParticipantId == participant.Id &&
                p.Id != pawn.Id &&
                p.Position == backwardPosition);

            if (backwardDestinationOccupied)
            {
                return BadRequest("Er staat al een eigen pion op deze positie.");
            }

            pawn.Progress = backwardProgress;
            pawn.Position = backwardPosition;

            card.IsPlayed = true;

            _context.GameActions.Add(new GameAction
            {
                GameId = game.Id,
                GameParticipantId = participant.Id,
                ActionType = GameActionType.MovePawn,
                PlayingCardId = card.Id,
                PawnId = pawn.Id
            });

            var backwardPlayers = game.Participants
                .OrderBy(p => p.SeatOrder)
                .ToList();

            int backwardCurrentIndex = backwardPlayers.FindIndex(
                p => p.Id == participant.Id);

            int backwardNextIndex = (backwardCurrentIndex + 1) % backwardPlayers.Count;

            game.CurrentPlayerId = backwardPlayers[backwardNextIndex].Id;
            game.Version++;

            await _context.SaveChangesAsync();

            return Ok("Je hebt vier stappen achteruit gezet.");
        }

        // Bereken de nieuwe voortgang
        int newProgress = pawn.Progress + movement;

        if (newProgress > 40)
        {
            return BadRequest("Je moet precies 40 stappen maken om te finishen.");
        }

        int startPosition = participant.SeatOrder * 10;
        int newPosition = (startPosition + newProgress) % 40;

        // Controleer of de pion finisht
        if (newProgress == 40)
        {
            pawn.Progress = 40;
            pawn.Position = null;
            pawn.IsFinished = true;
        }
        else
        {
            // Controleer eigen pion op bestemming
            bool ownPawnAtDestination = await _context.Pawns
                .AnyAsync(p =>
                    p.GameParticipantId == participant.Id &&
                    p.Id != pawn.Id &&
                    p.Position == newPosition);

            if (ownPawnAtDestination)
            {
                return BadRequest("Er staat al een eigen pion op deze positie.");
            }

            // Controleer of een startpion de route blokkeert
            for (int step = 1; step <= movement; step++)
            {
                int checkPosition =
                    (startPosition + pawn.Progress + step + 40) % 40;

                bool blockingPawn = await _context.Pawns
                    .Include(p => p.GameParticipant)
                    .AnyAsync(p =>
                        p.GameParticipant!.GameId == id &&
                        p.Id != pawn.Id &&
                        p.Position == checkPosition &&
                        (
                            // Eigen pion op het eigen startvak
                            (p.GameParticipantId == participant.Id &&
                            p.Position == startPosition &&
                            p.Progress == 0)

                            ||

                            // Tegenstander op zijn beschermde startvak
                            (p.Progress == 0 &&
                            p.Position == p.GameParticipant.SeatOrder * 10)
                        ));

                if (blockingPawn)
                {
                    return BadRequest("Een startpion blokkeert de doorgang.");
                }
            }
            // Zoek een tegenstander op de bestemming
            var opponentPawn = await _context.Pawns
                .Include(p => p.GameParticipant)
                .FirstOrDefaultAsync(p =>
                    p.GameParticipant!.GameId == id &&
                    p.GameParticipantId != participant.Id &&
                    p.Position == newPosition);

            if (opponentPawn != null)
            {
                int opponentStart = opponentPawn.GameParticipant!.SeatOrder * 10;

                // Beschermde startpositie mag niet worden geslagen
                if (opponentPawn.Position == opponentStart &&
                    opponentPawn.Progress == 0)
                {
                    return BadRequest("Dit startvak is beschermd.");
                }

                // Stuur de tegenstander terug naar de basis
                opponentPawn.Position = null;
                opponentPawn.Progress = 0;
            }

            // Verplaats je eigen pion
            pawn.Progress = newProgress;
            pawn.Position = newPosition;
        }

        // Controleer of alle vier pionnen gefinisht zijn
        bool allPawnsFinished = await _context.Pawns
            .Where(p => p.GameParticipantId == participant.Id)
            .AllAsync(p => p.IsFinished);

        if (allPawnsFinished)
        {
            game.Status = GameStatus.Finished;
            game.WinnerId = participant.Id;
            game.CurrentPlayerId = null;
        }

        // Markeer de kaart als gespeeld
        card.IsPlayed = true;

        // Registreer de actie
        _context.GameActions.Add(new GameAction
        {
            GameId = game.Id,
            GameParticipantId = participant.Id,
            ActionType = GameActionType.MovePawn,
            PlayingCardId = card.Id,
            PawnId = pawn.Id
        });

        // Geef de beurt door aan de volgende speler
        var players = game.Participants
            .OrderBy(p => p.SeatOrder)
            .ToList();

        int currentIndex = players.FindIndex(p => p.Id == participant.Id);
        int nextIndex = (currentIndex + 1) % players.Count;

        game.CurrentPlayerId = players[nextIndex].Id;
        game.Version++;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            Message = "Kaart succesvol gespeeld.",
            Card = card.Value,
            PawnId = pawn.Id,
            NewPosition = pawn.Position,
            NewProgress = pawn.Progress,
            NextPlayerId = game.CurrentPlayerId
        });
    }

    public GamesController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet("{id}/opponent-hand/{participantId}")]
    public async Task<IActionResult> GetOpponentHand(int id, int participantId)
    {
        var userId = _userManager.GetUserId(User);

        if (userId == null)
            return Unauthorized();

        var game = await _context.Games
            .Include(g => g.Participants)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (game == null)
            return NotFound("Spel niet gevonden.");

        if (game.Status != GameStatus.Active)
            return BadRequest("Dit spel is niet actief.");

        var participant = game.Participants.FirstOrDefault(p =>
            p.UserId == userId);

        if (participant == null)
            return Forbid();

        if (game.CurrentPlayerId != participant.Id)
            return BadRequest("Het is niet jouw beurt.");

        bool hasAce = await _context.PlayingCards.AnyAsync(c =>
            c.GameId == id &&
            c.OwnerId == participant.Id &&
            c.Value == CardValue.Ace &&
            !c.IsPlayed);

        if (!hasAce)
            return BadRequest("Je hebt geen Aas om te spelen.");

        var opponent = game.Participants.FirstOrDefault(p =>
            p.Id == participantId &&
            p.Id != participant.Id);

        if (opponent == null)
            return BadRequest("Ongeldige tegenstander.");

        var opponentHand = await _context.PlayingCards
            .Where(c =>
                c.GameId == id &&
                c.OwnerId == opponent.Id &&
                !c.IsPlayed)
            .Select(c => new
            {
                c.Id,
                c.Suit,
                c.Value
            })
            .ToListAsync();

        return Ok(opponentHand);
    }

    private async Task AdvanceTurnAsync(
    Game game,
    GameParticipant currentParticipant)
    {
        // Sla de huidige actie eerst op
        await _context.SaveChangesAsync();

        if (game.Status == GameStatus.Finished)
            return;

        var participants = game.Participants
            .OrderBy(p => p.SeatOrder)
            .ToList();

        // Tel hoeveel ongebruikte kaarten iedere speler heeft
        var handCounts = await _context.PlayingCards
            .Where(c => c.GameId == game.Id &&
                        c.OwnerId.HasValue &&
                        !c.IsPlayed)
            .GroupBy(c => c.OwnerId!.Value)
            .Select(g => new
            {
                ParticipantId = g.Key,
                Count = g.Count()
            })
            .ToDictionaryAsync(x => x.ParticipantId, x => x.Count);

        // Zijn alle handen leeg?
        if (handCounts.Values.Sum() == 0)
        {
            int cardsNeeded = participants.Count * 10;

            var drawPile = await _context.PlayingCards
                .Where(c => c.GameId == game.Id &&
                            c.OwnerId == null &&
                            !c.IsPlayed)
                .ToListAsync();

            // Onvoldoende kaarten? Verzamel alle 52 kaarten opnieuw.
            if (drawPile.Count < cardsNeeded)
            {
                drawPile = await _context.PlayingCards
                    .Where(c => c.GameId == game.Id)
                    .ToListAsync();

                foreach (var card in drawPile)
                {
                    card.OwnerId = null;
                    card.IsPlayed = false;
                }
            }

            // Schud de kaarten
            for (int i = drawPile.Count - 1; i > 0; i--)
            {
                int j = System.Security.Cryptography.RandomNumberGenerator
                    .GetInt32(i + 1);

                (drawPile[i], drawPile[j]) = (drawPile[j], drawPile[i]);
            }

            // Deel 10 kaarten per speler
            int cardIndex = 0;

            foreach (var player in participants)
            {
                for (int i = 0; i < 10; i++)
                {
                    drawPile[cardIndex].OwnerId = player.Id;
                    drawPile[cardIndex].IsPlayed = false;
                    cardIndex++;
                }
            }

            game.Round++;
            game.CurrentPlayerId = participants[0].Id;
        }
        else
        {
            // Zoek de volgende speler die nog kaarten heeft
            int currentIndex = participants.FindIndex(
                p => p.Id == currentParticipant.Id);

            for (int offset = 1; offset <= participants.Count; offset++)
            {
                var nextPlayer = participants[
                    (currentIndex + offset) % participants.Count];

                if (handCounts.TryGetValue(nextPlayer.Id, out int count) &&
                    count > 0)
                {
                    game.CurrentPlayerId = nextPlayer.Id;
                    break;
                }
            }
        }

        game.Version++;

        await _context.SaveChangesAsync();
    }
}