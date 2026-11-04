// Authenticatie en authorizatie met identity-framework
using Microsoft.AspNetCore.Identity;

// Spel/api/Models/ApplicationUser.cs
namespace Spel.Api.Models;

// Eigenschappen en functionaliteit erven van bestaande IdentityUser klasse
public class ApplicationUser : IdentityUser
{
}