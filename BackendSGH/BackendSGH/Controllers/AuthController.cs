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

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _context = context;
        }

        // -------------------------------------------------------------------------
        // INSCRIPTION CLIENT (Publique)
        // -------------------------------------------------------------------------
        [HttpPost("RegisterClient")]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterClient([FromBody] RegisterDto dto)
        {
            if (dto == null) return BadRequest("Requête vide.");

            // On force le type "Client"
            string userType = "Client"; 
            
            // Génération du pseudo
            string nomNettoye = dto.Nom.Trim();
            string prenomNettoye = dto.Prenom.Trim();
            string nomUtilisateurGenere = $"{prenomNettoye}{nomNettoye}";

            var user = new ApplicationUser 
            { 
                UserName = dto.Email, 
                Email = dto.Email,
                NomUtilisateur = nomUtilisateurGenere
            };

            // Création Identity
            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded) return BadRequest(result.Errors);

            // Rôle Identity
            if (!await _roleManager.RoleExistsAsync(userType)) await _roleManager.CreateAsync(new IdentityRole(userType));
            await _userManager.AddToRoleAsync(user, userType);

            // Création Profil Client
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
                // Rollback si la création du profil échoue
                await _userManager.DeleteAsync(user);
                return BadRequest($"Erreur création profil client : {ex.Message}");
            }

            return Ok(new { message = "Client inscrit avec succès", userId = user.Id, nomUtilisateur = nomUtilisateurGenere });
        }
        // -------------------------------------------------------------------------
        // CRÉATION RESPONSABLE 
        // -------------------------------------------------------------------------

        [HttpPost("CreateResponsable")]
        [Authorize(Roles = "Responsable")] 
        public async Task<IActionResult> CreateResponsable([FromBody] RegisterDto dto)
        {
            if (dto == null) return BadRequest("Requête vide.");

            
            // 1. Qui fait la demande ?
            var currentUserId = _userManager.GetUserId(User);
            var currentUserProfile = await _context.Responsables
                .FirstOrDefaultAsync(r => r.ApplicationUserId == currentUserId);

            if (currentUserProfile == null) return Unauthorized("Profil responsable introuvable.");

            // 2. Quel est son pouvoir ?
            bool isSuperAdmin = currentUserProfile.Role.EndsWith("/SUPERADMIN");
            bool isAdmin = currentUserProfile.Role.EndsWith("/ADMIN");

            // 3. Que veut-il créer ?
            string nouveauRole = dto.RoleEmploye?.Trim() ?? "Employé Standard";
            bool targetIsAdmin = nouveauRole.EndsWith("/ADMIN");
            bool targetIsSuperAdmin = nouveauRole.EndsWith("/SUPERADMIN");

            // 4. Règles de blocage
            // Règle : Si je ne suis ni Admin ni SuperAdmin, je ne crée rien.
            if (!isSuperAdmin && !isAdmin)
            {
                return StatusCode(403, "Accès refusé : Vous n'avez pas les droits de création.");
            }

            // Règle : Si je suis Admin (mais pas Super), je ne peux pas créer de chefs.
            if (isAdmin && (targetIsAdmin || targetIsSuperAdmin))
            {
                return StatusCode(403, "Accès refusé : Seul un SUPERADMIN peut nommer un nouvel ADMIN ou SUPERADMIN.");
            }

            // B. CRÉATION DU COMPTE --------------------------------------------

            string userType = "Responsable";
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
                var responsable = new Responsable
                {
                    ApplicationUserId = user.Id,
                    Role = nouveauRole // Le rôle validé par la logique de sécurité
                };
                _context.Responsables.Add(responsable);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await _userManager.DeleteAsync(user);
                return BadRequest($"Erreur création employé : {ex.Message}");
            }

            return Ok(new { message = $"Employé créé avec succès. Rôle : {nouveauRole}", nomUtilisateur = nomUtilisateurGenere });
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

            return Ok(new
            {
                Id = user.Id,
                Email = user.Email,
                NomUtilisateur = user.NomUtilisateur, 
                Roles = roles
            });
        }
    }
}