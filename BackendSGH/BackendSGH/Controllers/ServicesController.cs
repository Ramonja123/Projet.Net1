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

using BackendSGH.Services; // Added
using System.Text; // Added

namespace BackendSGH.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServicesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService; // Added

        public ServicesController(ApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // GET: api/Services
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Service>>> GetServices()
        {
            return await _context.Services.ToListAsync();
        }

        // GET: api/Services/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Service>> GetService(int id)
        {
            var service = await _context.Services.FindAsync(id);

            if (service == null)
            {
                return NotFound();
            }

            return service;
        }

        // PUT: api/Services/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Responsable")]
        public async Task<IActionResult> PutService(int id, [FromForm] BackendSGH.Models.Dtos.ServiceCreateDto dto)
        {
            if (!await IsAdmin()) return StatusCode(403, "Seul un administrateur peut modifier des services.");

            var service = await _context.Services.FindAsync(id);
            if (service == null)
            {
                return NotFound();
            }

            service.Nom = dto.Nom;
            service.Description = dto.Description;
            service.Prix = dto.Prix;
            service.TypeService = dto.TypeService;

            if (dto.Images != null && dto.Images.Count > 0)
            {
                try 
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    var imagePaths = new List<string>();
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
                    service.ImagePath = System.Text.Json.JsonSerializer.Serialize(imagePaths);
                }
                catch (Exception ex)
                {
                    return BadRequest($"Erreur lors de l'upload des images : {ex.Message}");
                }
            }

            _context.Entry(service).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ServiceExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Services
        [HttpPost]
        [Authorize(Roles = "Responsable")]
        public async Task<ActionResult<Service>> PostService([FromForm] BackendSGH.Models.Dtos.ServiceCreateDto dto)
        {
            if (!await IsAdmin()) return StatusCode(403, "Seul un administrateur peut ajouter des services.");

            var imagePaths = new List<string>();
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

            var service = new Service
            {
                Nom = dto.Nom,
                Description = dto.Description,
                Prix = dto.Prix,
                TypeService = dto.TypeService,
                ImagePath = imagePaths.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(imagePaths) : null
            };

            _context.Services.Add(service);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetService", new { id = service.Id }, service);
        }

        // DELETE: api/Services/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Responsable")]
        public async Task<IActionResult> DeleteService(int id)
        {
            if (!await IsAdmin()) return StatusCode(403, "Seul un administrateur peut supprimer des services.");

            var service = await _context.Services.FindAsync(id);
            if (service == null)
            {
                return NotFound();
            }

            _context.Services.Remove(service);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task<bool> IsAdmin()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var responsable = await _context.Responsables.FirstOrDefaultAsync(r => r.ApplicationUserId == userId);
            return responsable != null && responsable.Role.EndsWith("/ADMIN");
        }

        private bool ServiceExists(int id)
        {
            return _context.Services.Any(e => e.Id == id);
        }

        // POST: api/Services/reserve
        [HttpPost("reserve")]
        [Authorize]
        public async Task<IActionResult> ReserveService([FromBody] ReservationServiceRequestDto request)
        {
            var service = await _context.Services.FindAsync(request.ServiceId);
            if (service == null) return BadRequest("Service introuvable.");

            var reservation = new ReservationService
            {
                ServiceId = request.ServiceId,
                Date = request.Date,
                Heure = request.Heure,
                Prix = service.Prix,
                Statut = "Confirmée"
            };

            // If user is Client, link to ClientId
            if (User.IsInRole("Client"))
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var client = await _context.Clients.Include(c => c.ApplicationUser).FirstOrDefaultAsync(c => c.ApplicationUserId == userId);
                if (client != null) 
                {
                    reservation.ClientId = client.Id;
                    
                    // If service has cost, add to Panier
                    if (service.Prix > 0)
                    {
                        reservation.Statut = "Panier";
                        
                        var panier = await _context.Paniers
                            .FirstOrDefaultAsync(p => p.ClientId == client.Id && p.Statut == "Actif");
                        
                        if (panier == null)
                        {
                            panier = new Panier
                            {
                                ClientId = client.Id,
                                Statut = "Actif",
                                Total = 0
                            };
                            _context.Paniers.Add(panier);
                            await _context.SaveChangesAsync(); // Get ID
                        }
                        
                        reservation.PanierId = panier.Id;
                        panier.Total += (service.Prix ?? 0);
                    }
                    // Else: Free service, just confirm. No points for free service by rule? 
                    // Rule: "pour chaque 20 euro 1 point". 0 euro = 0 points.
                }
            }
            // If user is Responsable, check if they provided a client name or ID
            else if (User.IsInRole("Responsable"))
            {
                if (request.ClientId.HasValue)
                {
                    reservation.ClientId = request.ClientId.Value;
                }
                else if (!string.IsNullOrEmpty(request.NomClientNonInscrit))
                {
                    reservation.NomClientNonInscrit = request.NomClientNonInscrit;
                }
                else
                {
                    return BadRequest("Veuillez spécifier un client (ID ou Nom).");
                }
            }

            _context.ReservationServices.Add(reservation);
            await _context.SaveChangesAsync();

            if (reservation.Statut == "Panier")
                return Ok(new { message = "Service ajouté au panier !", id = reservation.Id, inCart = true });
            
            if (reservation.Statut == "Confirmée")
            {
                // SEND EMAIL
                try {
                    // We might need to fetch client if not already loaded (e.g. Responsable case)
                    Client resClient = null;
                    if (reservation.ClientId > 0)
                    {
                        resClient = await _context.Clients.Include(c => c.ApplicationUser).FirstOrDefaultAsync(c => c.Id == reservation.ClientId);
                    }

                    if (resClient != null && resClient.ApplicationUser != null)
                    {
                        var sb = new StringBuilder();
                        sb.Append($"<h1>Confirmation de Réservation Service</h1>");
                        sb.Append($"<p>Bonjour {resClient.Nom} {resClient.Prenom},</p>");
                        sb.Append($"<p>Votre réservation de service a été confirmée.</p>");
                        sb.Append($"<p><strong>Détails :</strong></p>");
                        sb.Append($"<ul>");
                        sb.Append($"<li><strong>Service :</strong> {service.Nom}</li>");
                        sb.Append($"<li><strong>Date :</strong> {request.Date:dd/MM/yyyy} à {request.Heure}</li>");
                        sb.Append($"<li><strong>Prix :</strong> {service.Prix} €</li>");
                        sb.Append($"</ul>");
                        sb.Append("<p>Cordialement,<br>L'équipe Blue Kasbah Resort</p>");

                        await _emailService.SendEmailAsync(resClient.ApplicationUser.Email, "Confirmation Service - Blue Kasbah", sb.ToString());
                    }
                }
                catch(Exception ex) { Console.WriteLine(ex.Message); }
                
                return Ok(new { message = "Réservation de service confirmée !", id = reservation.Id, inCart = false });
            }
            
            return Ok(new { message = "Réservation de service confirmée !", id = reservation.Id, inCart = false });
        }

        // GET: api/Services/reservations
        [HttpGet("reservations")]
        [Authorize(Roles = "Responsable")]
        public async Task<IActionResult> GetReservations()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var responsable = await _context.Responsables.FirstOrDefaultAsync(r => r.ApplicationUserId == userId);
            
            if (responsable == null) return Unauthorized();

            IQueryable<ReservationService> query = _context.ReservationServices
                .Include(r => r.Service)
                .Include(r => r.Client);

            // Filter by scope
            if (responsable.ServiceId.HasValue)
            {
                query = query.Where(r => r.ServiceId == responsable.ServiceId.Value);
            }
            // If responsible for rooms, maybe they shouldn't see service reservations? 
            // Or maybe they can see all? The prompt implies "gerer le service si il est respo dun certain service".
            // So if ServiceId is null, they might not see anything here unless they are Admin.
            else if (!responsable.Role.EndsWith("/ADMIN"))
            {
                // If not assigned to a service and not admin, return empty or error?
                // Assuming "Responsable Chambre" doesn't manage services.
                return Ok(new List<ReservationService>());
            }

            var list = await query.OrderByDescending(r => r.Date).ToListAsync();
            return Ok(list);
        }

        // GET: api/Services/my-reservations
        [HttpGet("my-reservations")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> GetMyServiceReservations()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.ApplicationUserId == userId);

            if (client == null) return BadRequest("Client introuvable.");

            var reservations = await _context.ReservationServices
                .Include(r => r.Service)
                // Only show confirmed or completed reservations, not those suspended in cart
                .Where(r => r.ClientId == client.Id && (r.Statut == "Confirmée" || r.Statut == "Terminée"))
                .OrderByDescending(r => r.Date)
                .Select(r => new
                {
                    r.Id,
                    r.Date,
                    r.Heure,
                    r.Prix,
                    r.Statut,
                    ServiceNom = r.Service.Nom,
                    Image = r.Service.ImagePath
                })
                .ToListAsync();

            return Ok(reservations);
        }
        [HttpPost("complete/{id}")]
        [Authorize(Roles = "Responsable")]
        public async Task<IActionResult> CompleteService(int id)
        {
            var reservation = await _context.ReservationServices
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null) return NotFound("Réservation de service introuvable.");

            if (reservation.Statut == "Terminée")
                return BadRequest("Le service est déjà terminé.");

            // 1. Update Status
            reservation.Statut = "Terminée";

            // Points awarded at creation.

            await _context.SaveChangesAsync();

            return Ok(new { message = "Service marqué comme terminé !" });
        }
    }

    public class ReservationServiceRequestDto
    {
        public int ServiceId { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan Heure { get; set; }
        public int? ClientId { get; set; }
        public string? NomClientNonInscrit { get; set; }
    }
}
