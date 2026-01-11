using Microsoft.AspNetCore.Identity;

namespace BackendSGH.Models
{
    public class ApplicationUser : IdentityUser
    {
        public required string NomUtilisateur { get; set; }
// Un utilisateur PEUT avoir un dossier Client (s'il réserve)
public Client? ClientProfile { get; set; }

        // Un utilisateur PEUT avoir un dossier Responsable (s'il travaille ici)
        public Responsable? ResponsableProfile { get; set; }

        public string? PasswordResetCode { get; set; }
        public DateTime? PasswordResetCodeExpires { get; set; }
    }
}