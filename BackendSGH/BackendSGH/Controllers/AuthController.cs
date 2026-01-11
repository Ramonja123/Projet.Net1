using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; 
using BackendSGH.Models;
using BackendSGH.Models.Dtos;
using BackendSGH.Services; // Added

namespace BackendSGH.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService; // Added

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            IEmailService emailService) // Added
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _context = context;
            _emailService = emailService;
        }


        [HttpPost("RegisterClient")]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterClient([FromBody] RegisterDto dto)
        {
            if (dto == null) return BadRequest("Requête vide.");

            string userType = "Client"; 
            
            string nomNettoye = dto.Nom.Trim();
            string prenomNettoye = dto.Prenom.Trim();
            string nomUtilisateurGenere = $"{prenomNettoye}{nomNettoye}";

            var user = new ApplicationUser 
            { 
                UserName = dto.Email, 
                Email = dto.Email,
                NomUtilisateur = nomUtilisateurGenere
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded) return BadRequest(result.Errors);

            if (!await _roleManager.RoleExistsAsync(userType)) await _roleManager.CreateAsync(new IdentityRole(userType));
            await _userManager.AddToRoleAsync(user, userType);

            try 
            {
                var client = new Client
                {
                    ApplicationUserId = user.Id,
                    Adresse = dto.Adresse ?? "",
                    DateNaissance = dto.DateNaissance ?? DateTime.Now,
                    PointsFidelite = 0,
                    Nom = dto.Nom,
                    Prenom = dto.Prenom
                };
                _context.Clients.Add(client);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await _userManager.DeleteAsync(user);
                return BadRequest($"Erreur création profil client : {ex.Message}");
            }

            return Ok(new { message = "Client inscrit avec succès", userId = user.Id, nomUtilisateur = nomUtilisateurGenere });
        }

        [HttpPost("RegisterAdmin")]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterAdmin([FromBody] RegisterDto dto)
        {
            if (dto == null) return BadRequest("Requête vide.");

            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                NomUtilisateur = $"{dto.Prenom}{dto.Nom}"
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded) return BadRequest(result.Errors);

            string userType = "Responsable";
            if (!await _roleManager.RoleExistsAsync(userType)) await _roleManager.CreateAsync(new IdentityRole(userType));
            await _userManager.AddToRoleAsync(user, userType);

            var responsable = new Responsable
            {
                ApplicationUserId = user.Id,
                Role = "Directeur/ADMIN" 
            };
            _context.Responsables.Add(responsable);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Admin créé avec succès" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null) return Unauthorized("Email ou mot de passe incorrect");

            var passwordValid = await _userManager.CheckPasswordAsync(user, dto.Password);
            if (!passwordValid) return Unauthorized("Email ou mot de passe incorrect");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Email ?? ""),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim("NomUtilisateur", user.NomUtilisateur ?? "")
            };

            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var claimsIdentity = new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme);
            await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, new ClaimsPrincipal(claimsIdentity));

            return Ok(new { message = "Connecté", nom = user.NomUtilisateur, roles = roles });
        }


        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return Ok(new { message = "Déconnexion réussie" });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var roles = await _userManager.GetRolesAsync(user);

            var client = await _context.Clients.FirstOrDefaultAsync(c => c.ApplicationUserId == user.Id);
            var points = client?.PointsFidelite ?? 0;

            return Ok(new
            {
                Id = user.Id,
                Email = user.Email,
                NomUtilisateur = user.NomUtilisateur, 
                Roles = roles,
                PointsFidelite = points,
                Nom = client?.Nom,
                Prenom = client?.Prenom,
                Adresse = client?.Adresse,
                DateNaissance = client?.DateNaissance
            });
        }

        [HttpPut("profile")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> UpdateProfile([FromBody] ClientUpdateDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var client = await _context.Clients.FirstOrDefaultAsync(c => c.ApplicationUserId == user.Id);
            if (client == null) return BadRequest("Profil client introuvable.");

            // Update User fields
            // Assuming we don't change email/username here for simplicity, or handle it carefully.
            // user.NomUtilisateur = ... ?

            // Update Client fields
            client.Nom = dto.Nom;
            client.Prenom = dto.Prenom;
            client.Adresse = dto.Adresse;
            client.DateNaissance = dto.DateNaissance;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Profil mis à jour avec succès" });
        }
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            if (string.IsNullOrEmpty(dto.Email)) return BadRequest("Email requis.");

            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null) return Ok(new { message = "Si l'email existe, un code a été envoyé." }); // Security: Don't reveal user existence

            // Generate 6-digit code
            var code = new Random().Next(100000, 999999).ToString();
            user.PasswordResetCode = code;
            user.PasswordResetCodeExpires = DateTime.UtcNow.AddMinutes(15);
            
            await _userManager.UpdateAsync(user);

            // Send Email
            var subject = "Réinitialisation de mot de passe - Blue Kasbah";
            var body = $"<h1>Code de rénitialisation</h1><p>Votre code est : <strong>{code}</strong></p><p>Ce code expire dans 15 minutes.</p>";
            
            try 
            {
                await _emailService.SendEmailAsync(user.Email, subject, body);
            }
            catch(Exception ex)
            {
                // Create a basic fallback or log
                Console.WriteLine("Email error: " + ex.Message);
            }

            return Ok(new { message = "Code envoyé." });
        }

        [HttpPost("verify-code")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyCode([FromBody] VerifyCodeDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null) return BadRequest("Code invalide ou expiré.");

            if (user.PasswordResetCode != dto.Code || user.PasswordResetCodeExpires < DateTime.UtcNow)
            {
                 return BadRequest("Code invalide ou expiré.");
            }

            return Ok(new { message = "Code valide." });
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null) return BadRequest("Erreur.");

            // Verify again
            if (user.PasswordResetCode != dto.Code || user.PasswordResetCodeExpires < DateTime.UtcNow)
            {
                 return BadRequest("Code invalide ou expiré.");
            }

            // Reset Password
            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, code, dto.NewPassword);
            
            if (result.Succeeded)
            {
                user.PasswordResetCode = null;
                user.PasswordResetCodeExpires = null;
                await _userManager.UpdateAsync(user);
                return Ok(new { message = "Mot de passe réinitialisé avec succès." });
            }

            return BadRequest(result.Errors);
        }

    }

    public class ClientUpdateDto
    {
        public string Nom { get; set; }
        public string Prenom { get; set; }
        public string Adresse { get; set; }
        public DateTime DateNaissance { get; set; }
    }

    public class ForgotPasswordDto { public string Email { get; set; } }
    public class VerifyCodeDto { public string Email { get; set; } public string Code { get; set; } }
    public class ResetPasswordDto { public string Email { get; set; } public string Code { get; set; } public string NewPassword { get; set; } }
}