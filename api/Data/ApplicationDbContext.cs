using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Spel.Api.Models;

namespace Spel.Api.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Game> Games { get; set; }

    public DbSet<GameParticipant> GameParticipants { get; set; }

    public DbSet<Pawn> Pawns { get; set; }

    public DbSet<PlayingCard> PlayingCards { get; set; }

    public DbSet<GameAction> GameActions { get; set; }

    // Configureren van de relaties tussen de entiteiten
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Game>()
            .HasOne(g => g.Creator)
            .WithMany()
            .HasForeignKey(g => g.CreatorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GameParticipant>()
            .HasOne(gp => gp.User)
            .WithMany()
            .HasForeignKey(gp => gp.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GameParticipant>()
            .HasIndex(gp => new { gp.GameId, gp.UserId })
            .IsUnique();

        modelBuilder.Entity<GameParticipant>()
            .HasOne(gp => gp.Game)
            .WithMany(g => g.Participants)
            .HasForeignKey(gp => gp.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Pawn>()
            .HasOne(p => p.GameParticipant)
            .WithMany(gp => gp.Pawns)
            .HasForeignKey(p => p.GameParticipantId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlayingCard>()
            .HasOne(pc => pc.Game)
            .WithMany(g => g.Cards)
            .HasForeignKey(pc => pc.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlayingCard>()
            .HasOne(pc => pc.Owner)
            .WithMany(gp => gp.Cards)
            .HasForeignKey(pc => pc.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GameAction>()
            .HasOne(ga => ga.Game)
            .WithMany(g => g.Actions)
            .HasForeignKey(ga => ga.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GameAction>()
            .HasOne(ga => ga.GameParticipant)
            .WithMany(gp => gp.Actions)
            .HasForeignKey(ga => ga.GameParticipantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GameAction>()
            .HasOne(ga => ga.PlayingCard)
            .WithMany()
            .HasForeignKey(ga => ga.PlayingCardId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GameAction>()
            .HasOne(ga => ga.Pawn)
            .WithMany()
            .HasForeignKey(ga => ga.PawnId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GameAction>()
            .HasOne(ga => ga.TargetPawn)
            .WithMany()
            .HasForeignKey(ga => ga.TargetPawnId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GameParticipant>()
            .HasIndex(gp => new { gp.GameId, gp.SeatOrder })
            .IsUnique();

        modelBuilder.Entity<GameParticipant>()
            .HasIndex(gp => new { gp.GameId, gp.Color })
            .IsUnique();

        modelBuilder.Entity<Game>()
            .HasOne(g => g.CurrentPlayer)
            .WithMany()
            .HasForeignKey(g => g.CurrentPlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Game>()
            .HasOne(g => g.Winner)
            .WithMany()
            .HasForeignKey(g => g.WinnerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Game>()
            .Property(g => g.Version)
            .IsConcurrencyToken();
    }
}