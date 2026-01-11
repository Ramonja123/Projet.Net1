using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BackendSGH.Models;

using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace BackendSGH.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TypeChambresController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TypeChambresController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/TypeChambres
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TypeChambre>>> GetTypeChambres()
        {
            return await _context.TypeChambres.ToListAsync();
        }

        // GET: api/TypeChambres/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TypeChambre>> GetTypeChambre(int id)
        {
            var typeChambre = await _context.TypeChambres.FindAsync(id);

            if (typeChambre == null)
            {
                return NotFound();
            }

            return typeChambre;
        }

        // PUT: api/TypeChambres/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Responsable")]
        public async Task<IActionResult> PutTypeChambre(int id, TypeChambre typeChambre)
        {
            if (!await IsAdmin()) return StatusCode(403, "Seul un administrateur peut modifier les types de chambres.");
            
            if (id != typeChambre.Id) return BadRequest();
            _context.Entry(typeChambre).State = EntityState.Modified;
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { if (!TypeChambreExists(id)) return NotFound(); else throw; }
            return NoContent();
        }

        // POST: api/TypeChambres
        [HttpPost]
        [Authorize(Roles = "Responsable")]
        public async Task<ActionResult<TypeChambre>> PostTypeChambre([FromForm] BackendSGH.Models.Dtos.TypeChambreCreateDto dto)
        {
            if (!await IsAdmin()) return StatusCode(403, "Seul un administrateur peut ajouter des types de chambres.");

            var imagePaths = new List<string>();
            // ... (existing logic)
            if (dto.Images != null && dto.Images.Count > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                foreach (var file in dto.Images)
                {
                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }
                    imagePaths.Add("/images/" + uniqueFileName);
                }
            }

            var typeChambre = new TypeChambre
            {
                Nom = dto.Nom,
                Description = dto.Description,
                Capacite = dto.Capacite,
                Tarif = dto.Tarif,
                Vue = dto.Vue,
                ImagePath = System.Text.Json.JsonSerializer.Serialize(imagePaths)
            };

            _context.TypeChambres.Add(typeChambre);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetTypeChambre", new { id = typeChambre.Id }, typeChambre);
        }

        // DELETE: api/TypeChambres/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Responsable")]
        public async Task<IActionResult> DeleteTypeChambre(int id)
        {
            if (!await IsAdmin()) return StatusCode(403, "Seul un administrateur peut supprimer des types de chambres.");

            var typeChambre = await _context.TypeChambres.FindAsync(id);
            if (typeChambre == null) return NotFound();

            _context.TypeChambres.Remove(typeChambre);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task<bool> IsAdmin()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var responsable = await _context.Responsables.FirstOrDefaultAsync(r => r.ApplicationUserId == userId);
            return responsable != null && responsable.Role.EndsWith("/ADMIN");
        }

        private bool TypeChambreExists(int id)
        {
            return _context.TypeChambres.Any(e => e.Id == id);
        }

        // GET: api/TypeChambres/search
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<TypeChambre>>> SearchTypeChambres(DateTime? dateArrivee, DateTime? dateDepart, int? capacite)
        {
            var query = _context.TypeChambres.AsQueryable();

            if (capacite.HasValue)
            {
                query = query.Where(t => t.Capacite >= capacite.Value);
            }

            var typeChambres = await query.Include(t => t.Chambres).ToListAsync();

            if (dateArrivee.HasValue && dateDepart.HasValue)
            {
                if (dateArrivee >= dateDepart)
                {
                    return BadRequest("La date d'arrivée doit être antérieure à la date de départ.");
                }

                // Find reserved room IDs during the requested period
                var reservedChambreIds = await _context.ReservationChambres
                    .Where(r => r.Statut != "Annulée" && 
                                r.DateDebut < dateDepart.Value && 
                                r.DateFin > dateArrivee.Value)
                    .Select(r => r.ChambreId)
                    .Distinct()
                    .ToListAsync();

                // Filter TypeChambres that have at least one available room
                typeChambres = typeChambres.Where(t => t.Chambres.Any(c => !reservedChambreIds.Contains(c.Id))).ToList();
            }

            return typeChambres;
        }
    }
}
