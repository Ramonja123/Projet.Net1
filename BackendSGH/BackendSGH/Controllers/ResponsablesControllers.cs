using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BackendSGH.Models;
using Microsoft.AspNetCore.Authorization;
using BackendSGH.Models.Dtos;
using Microsoft.AspNetCore.Identity;

namespace BackendSGH.Controllers
{
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

// GET: Responsables
[HttpGet("Responsables")] 
public async Task<IActionResult> Index()
{
        var currentUserId = _userManager.GetUserId(User);
    var currentUserProfile = await _context.Responsables
        .FirstOrDefaultAsync(r => r.ApplicationUserId == currentUserId);

    if (currentUserProfile == null) return Unauthorized("Profil responsable introuvable.");
        bool isAdmin = currentUserProfile.Role.EndsWith("/ADMIN");
    
 
    

    if (!isAdmin)
    {
        return StatusCode(403, "Accès refusé .");
    }
    // 1. On récupère les données de la base
    var rawList = await _context.Responsables
        .Include(r => r.ApplicationUser)
        .Select(r => new 
        {
            Id = r.Id,
            FullRole = r.Role, 
            NomUtilisateur = r.ApplicationUser.NomUtilisateur,
            Email = r.ApplicationUser.Email,
            UserId = r.ApplicationUserId
        })
        .ToListAsync();

    var resultats = rawList.Select(r => new 
    {
        r.Id,
        Role = r.FullRole.Contains("/") ? r.FullRole.Split('/')[0] : r.FullRole,
        r.NomUtilisateur,
        r.Email,
        r.UserId
    });

    return Ok(resultats);
}

        // GET: Responsables/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var responsable = await _context.Responsables
                .Include(r => r.ApplicationUser)
                .FirstOrDefaultAsync(m => m.Id == id);
                
            if (responsable == null) return NotFound();

            return View(responsable);
        }


        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

[HttpPost]
[Route("Responsables/Create")]
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
            
            var roleResult = await _userManager.AddToRoleAsync(user, userType);
            if (!roleResult.Succeeded) throw new Exception("Erreur assignation rôle.");

            var responsable = new Responsable
            {
                ApplicationUserId = user.Id,
                Role = nouveauRole
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

        // GET: Responsables/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var responsable = await _context.Responsables.FindAsync(id);
            if (responsable == null) return NotFound();
            
            ViewData["ApplicationUserId"] = new SelectList(_context.Users, "Id", "Id", responsable.ApplicationUserId);
            return View(responsable);
        }

        // POST: Responsables/Edit/5
        [HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Edit(int id, Responsable responsableModifie)
{
    if (id != responsableModifie.Id) return NotFound();

    var responsableOriginal = await _context.Responsables
        .AsNoTracking()
        .FirstOrDefaultAsync(r => r.Id == id);

    if (responsableOriginal == null) return NotFound();


    responsableModifie.Role = responsableOriginal.Role;
    
    responsableModifie.ApplicationUserId = responsableOriginal.ApplicationUserId;

    if (ModelState.IsValid)
    {
        _context.Update(responsableModifie);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
    return View(responsableModifie);
}

        // GET: Responsables/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var responsable = await _context.Responsables
                .Include(r => r.ApplicationUser)
                .FirstOrDefaultAsync(m => m.Id == id);
                
            if (responsable == null) return NotFound();

            return View(responsable);
        }

        // POST: Responsables/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var responsable = await _context.Responsables.FindAsync(id);
            if (responsable != null)
            {
                _context.Responsables.Remove(responsable);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        // POST: Responsables/Promote/5
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

        private bool ResponsableExists(int id)
        {
            return _context.Responsables.Any(e => e.Id == id);
        }
    }
}