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
    public class ChambresController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ChambresController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Chambres
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Chambre>>> GetChambres()
        {
            return await _context.Chambres.Include(c => c.TypeChambre).ToListAsync();
        }

        // GET: api/Chambres/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Chambre>> GetChambre(int id)
        {
            var chambre = await _context.Chambres.Include(c => c.TypeChambre).FirstOrDefaultAsync(c => c.Id == id);

            if (chambre == null)
            {
                return NotFound();
            }

            return chambre;
        }

        // PUT: api/Chambres/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Responsable")]
        public async Task<IActionResult> PutChambre(int id, Chambre chambre)
        {
            // Managers (Responsable Chambre) and Admins can update (e.g. state)
            // We could add a check here to ensure they are Responsable Chambre if we wanted to be strict.
            if (id != chambre.Id) return BadRequest();
            _context.Entry(chambre).State = EntityState.Modified;
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { if (!ChambreExists(id)) return NotFound(); else throw; }
            return NoContent();
        }

        // POST: api/Chambres
        [HttpPost]
        [Authorize(Roles = "Responsable")]
        public async Task<ActionResult<Chambre>> PostChambre(Chambre chambre)
        {
            if (!await IsAdmin()) return StatusCode(403, "Seul un administrateur peut ajouter des chambres.");

            _context.Chambres.Add(chambre);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetChambre", new { id = chambre.Id }, chambre);
        }

        // DELETE: api/Chambres/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Responsable")]
        public async Task<IActionResult> DeleteChambre(int id)
        {
            if (!await IsAdmin()) return StatusCode(403, "Seul un administrateur peut supprimer des chambres.");

            var chambre = await _context.Chambres.FindAsync(id);
            if (chambre == null) return NotFound();

            _context.Chambres.Remove(chambre);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task<bool> IsAdmin()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var responsable = await _context.Responsables.FirstOrDefaultAsync(r => r.ApplicationUserId == userId);
            return responsable != null && responsable.Role.EndsWith("/ADMIN");
        }

        private bool ChambreExists(int id)
        {
            return _context.Chambres.Any(e => e.Id == id);
        }
    }
}
