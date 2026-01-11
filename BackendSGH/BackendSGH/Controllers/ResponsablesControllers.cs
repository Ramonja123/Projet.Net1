using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BackendSGH.Models;
using Microsoft.AspNetCore.Authorization;
using BackendSGH.Models.Dtos;
using Microsoft.AspNetCore.Identity;

namespace BackendSGH.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ResponsablesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public ResponsablesController(
            ApplicationDbContext context, 
            UserManager<ApplicationUser> userManager, 
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager; 
            _roleManager = roleManager; 
        }

        // GET: api/Responsables
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var currentUserId = _userManager.GetUserId(User);
            var currentUserProfile = await _context.Responsables
                .FirstOrDefaultAsync(r => r.ApplicationUserId == currentUserId);

            if (currentUserProfile == null) return Unauthorized("Profil responsable introuvable.");
            
            bool isAdmin = currentUserProfile.Role.EndsWith("/ADMIN");

            if (!isAdmin)
            {
                return StatusCode(403, "Accès refusé.");
            }

            var rawList = await _context.Responsables
                .Include(r => r.ApplicationUser)
                .Include(r => r.Service)
                .Select(r => new 
                {
                    Id = r.Id,
                    FullRole = r.Role, 
                    NomUtilisateur = r.ApplicationUser.NomUtilisateur,
                    Email = r.ApplicationUser.Email,
                    UserId = r.ApplicationUserId,
                    Service = r.Service != null ? r.Service.Nom : "N/A",
                    ServiceId = r.ServiceId,
                    IsResponsableChambre = r.IsResponsableChambre
                })
                .ToListAsync();

            var resultats = rawList.Select(r => new 
            {
                r.Id,
                Role = r.FullRole.Contains("/") ? r.FullRole.Split('/')[0] : r.FullRole,
                r.NomUtilisateur,
                r.Email,
                r.UserId,
                Scope = r.IsResponsableChambre ? "Chambres" : (r.Service != "N/A" ? $"Service: {r.Service}" : "Aucun"),
                r.ServiceId,
                r.IsResponsableChambre
            });

            return Ok(resultats);
        }

        // GET: api/Responsables/me
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var currentUserId = _userManager.GetUserId(User);
            var r = await _context.Responsables
                .Include(r => r.ApplicationUser)
                .Include(r => r.Service)
                .FirstOrDefaultAsync(m => m.ApplicationUserId == currentUserId);
                
            if (r == null) return Unauthorized();

            return Ok(new 
            {
                r.Id,
                r.Role,
                r.IsResponsableChambre,
                ServiceId = r.ServiceId,
                ServiceName = r.Service?.Nom,
                Nom = r.ApplicationUser.NomUtilisateur
            });
        }

        // GET: api/Responsables/5
        [HttpGet("{id}")]
        public async Task<IActionResult> Details(int id)
        {
            var responsable = await _context.Responsables
                .Include(r => r.ApplicationUser)
                .FirstOrDefaultAsync(m => m.Id == id);
                
            if (responsable == null) return NotFound();

            return Ok(responsable);
        }

        // POST: api/Responsables/Create
        [HttpPost("Create")]
        [Authorize(Roles = "Responsable")] 
        public async Task<IActionResult> Create([FromBody] RegisterDto dto)
        {
            if (dto == null) return BadRequest("Requête vide.");

            var currentUserId = _userManager.GetUserId(User);
            var currentUserProfile = await _context.Responsables
                .FirstOrDefaultAsync(r => r.ApplicationUserId == currentUserId);

            if (currentUserProfile == null) return Unauthorized("Profil responsable introuvable.");

            bool isAdmin = currentUserProfile.Role.EndsWith("/ADMIN");
            
            string nouveauRole = dto.RoleEmploye?.Trim() ?? "Employé Standard";

            if (!isAdmin)
            {
                return StatusCode(403, "Accès refusé : Seul un administrateur peut créer des employés.");
            }

            string userType = "Responsable";
            string baseUserName = $"{dto.Prenom.Trim()}{dto.Nom.Trim()}".Replace(" ", "");
            
            string nomUtilisateurGenere = baseUserName;
            int counter = 1;
            while (await _userManager.FindByNameAsync(nomUtilisateurGenere) != null)
            {
                nomUtilisateurGenere = $"{baseUserName}{counter}";
                counter++;
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var user = new ApplicationUser 
                    { 
                        UserName = nomUtilisateurGenere, 
                        Email = dto.Email,
                        NomUtilisateur = nomUtilisateurGenere
                    };

                    var result = await _userManager.CreateAsync(user, dto.Password);
                    if (!result.Succeeded) return BadRequest(result.Errors);

                    if (!await _roleManager.RoleExistsAsync(userType)) 
                        await _roleManager.CreateAsync(new IdentityRole(userType));
                    
                    await _userManager.AddToRoleAsync(user, userType);

                    var responsable = new Responsable
                    {
                        ApplicationUserId = user.Id,
                        Role = nouveauRole,
                        ServiceId = dto.ServiceId,
                        IsResponsableChambre = dto.IsResponsableChambre
                    };
                    
                    _context.Responsables.Add(responsable);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    return Ok(new { 
                        message = $"Employé créé avec succès.", 
                        nomUtilisateur = nomUtilisateurGenere,
                        role = nouveauRole
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return BadRequest($"Erreur : {ex.Message}");
                }
            }
        }

        // DELETE: api/Responsables/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var responsable = await _context.Responsables.FindAsync(id);
            if (responsable == null) return NotFound();

            _context.Responsables.Remove(responsable);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Supprimé avec succès" });
        }

        // PUT: api/Responsables/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Responsable")]
        public async Task<IActionResult> UpdateResponsable(int id, [FromBody] ResponsableUpdateDto dto)
        {
            var currentUserId = _userManager.GetUserId(User);
            var currentUserProfile = await _context.Responsables.FirstOrDefaultAsync(r => r.ApplicationUserId == currentUserId);
            
            if (currentUserProfile == null || !currentUserProfile.Role.EndsWith("/ADMIN")) 
                return StatusCode(403, "Accès refusé.");

            var responsable = await _context.Responsables.FindAsync(id);
            if (responsable == null) return NotFound();

            // Preserve /ADMIN if it exists
            bool wasAdmin = responsable.Role.EndsWith("/ADMIN");
            responsable.Role = dto.Role + (wasAdmin ? "/ADMIN" : "");
            responsable.ServiceId = dto.ServiceId;
            responsable.IsResponsableChambre = dto.IsResponsableChambre;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST: api/Responsables/Promote/5
        [HttpPost("Promote/{id}")]
        [Authorize(Roles = "Responsable")]
        public async Task<IActionResult> PromoteToAdmin(int id)
        {
            var currentUserId = _userManager.GetUserId(User);
            var currentUserProfile = await _context.Responsables
                .FirstOrDefaultAsync(r => r.ApplicationUserId == currentUserId);

            if (currentUserProfile == null) return Unauthorized();

            if (!currentUserProfile.Role.EndsWith("/ADMIN"))
            {
                return StatusCode(403, "Accès refusé : Seul un administrateur peut promouvoir un employé.");
            }

            var targetResponsable = await _context.Responsables.FindAsync(id);
            if (targetResponsable == null) return NotFound("Employé introuvable.");


            if (targetResponsable.Role.EndsWith("/ADMIN"))
            {
                return BadRequest("Cet employé est déjà Administrateur.");
            }

            targetResponsable.Role = targetResponsable.Role + "/ADMIN";

            try 
            {
                _context.Update(targetResponsable);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return BadRequest($"Erreur lors de la promotion : {ex.Message}");
            }

            return Ok(new { 
                message = "Promotion réussie !", 
                nouveauRole = targetResponsable.Role 
            });
        }
    }

    public class ResponsableUpdateDto
    {
        public string Role { get; set; }
        public int? ServiceId { get; set; }
        public bool IsResponsableChambre { get; set; }
    }
}