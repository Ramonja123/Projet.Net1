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