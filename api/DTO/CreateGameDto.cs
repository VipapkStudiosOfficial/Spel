using System.ComponentModel.DataAnnotations;

namespace Spel.Api.DTOs;

public class CreateGameDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}