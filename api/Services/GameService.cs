using Microsoft.EntityFrameworkCore;
using Spel.Api.Data;
using Spel.Api.Models;

namespace Spel.Api.Services;

public class GameService
{
    private readonly ApplicationDbContext _context;

    public GameService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<(bool IsValid, string Error, int NewProgress,
        int? NewPosition, Pawn? CapturedPawn)> ValidateForwardMoveAsync(
        GameParticipant participant, Pawn pawn, int movement)
    {
        int newProgress = pawn.Progress + movement;

        // Je mag niet verder dan precies 40 stappen
        if (newProgress > 40)
        {
            return (false, "Je moet precies 40 stappen maken om te finishen.",
                0, null, null);
        }

        int startPosition = participant.SeatOrder * 10;

        // Bij precies 40 stappen is de pion gefinisht
        if (newProgress == 40)
        {
            return (true, "", 40, null, null);
        }

        int newPosition = (startPosition + newProgress) % 40;

        var pawns = await _context.Pawns
            .Include(p => p.GameParticipant)
            .Where(p => p.Position != null)
            .ToListAsync();

        Pawn? capturedPawn = null;

        // Controleer alle vakjes waar de pion langs beweegt
        for (int step = pawn.Progress + 1; step <= newProgress; step++)
        {
            int position = (startPosition + step) % 40;

            var blockingPawn = pawns.FirstOrDefault(p =>
                p.Id != pawn.Id && p.Position == position);

            if (blockingPawn == null)
            {
                continue;
            }

            var blockingParticipant = blockingPawn.GameParticipant!;

            int blockingStart = blockingParticipant.SeatOrder * 10;

            // Een pion op zijn eigen startpositie is beschermd
            if (blockingPawn.Position == blockingStart &&
                blockingPawn.Progress == 0)
            {
                return (false, "Een beschermde startpositie blokkeert je.",
                    0, null, null);
            }

            // Eigen pionnen mogen niet op hetzelfde vak eindigen
            if (position == newPosition &&
                blockingPawn.GameParticipantId == participant.Id)
            {
                return (false, "Er staat al een eigen pion op deze positie.",
                    0, null, null);
            }

            // Een tegenstander op de eindpositie wordt geslagen
            if (position == newPosition)
            {
                capturedPawn = blockingPawn;
            }
        }

        return (true, "", newProgress, newPosition, capturedPawn);
    }
}