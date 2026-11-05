using System.ComponentModel.DataAnnotations;

namespace Spel.Api.DTO;

public class CreateGameDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}