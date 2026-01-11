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
    public class ReservationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService; // Added

        public ReservationsController(ApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // GET: api/Reservations/check-availability
        [HttpGet("check-availability")]
        public async Task<IActionResult> CheckAvailability(int typeId, DateTime startDate, DateTime endDate)
        {
            if (startDate >= endDate)
            {
                return BadRequest("La date de début doit être antérieure à la date de fin.");
            }

            // 1. Get all rooms of this type
            var rooms = await _context.Chambres
                .Where(c => c.TypeChambreId == typeId)
                .ToListAsync();

            if (!rooms.Any())
            {
                return Ok(new { available = false, message = "Aucune chambre de ce type n'existe." });
            }

            // 2. Find rooms that are booked during this period
            // Overlap logic: (StartA < EndB) and (EndA > StartB)
            var bookedRoomIds = await _context.ReservationChambres
                .Where(r => r.Chambre.TypeChambreId == typeId && 
                            r.Statut != "Annulée" &&
                            r.DateDebut < endDate && 
                            r.DateFin > startDate)
                .Select(r => r.ChambreId)
                .Distinct()
                .ToListAsync();

            // 3. Filter available rooms
            var availableRooms = rooms.Where(r => !bookedRoomIds.Contains(r.Id)).ToList();

            return Ok(new 
            { 
                available = availableRooms.Any(), 
                count = availableRooms.Count,
                availableRoomIds = availableRooms.Select(r => r.Id).ToList()
            });
        }

        // GET: api/Reservations/unavailable-dates
        [HttpGet("unavailable-dates")]
        public async Task<IActionResult> GetUnavailableDates(int typeId, DateTime start, DateTime end)
        {
            // 1. Get all rooms of this type
            var rooms = await _context.Chambres
                .Where(c => c.TypeChambreId == typeId)
                .Select(c => c.Id)
                .ToListAsync();

            if (!rooms.Any()) return Ok(new List<string>()); // No rooms = effectively unavailable? Or handled elsewhere.

            int totalRooms = rooms.Count;

            // 2. Get reservations in range
            var reservations = await _context.ReservationChambres
                .Where(r => r.Chambre.TypeChambreId == typeId &&
                            r.Statut != "Annulée" &&
                            r.DateDebut < end &&
                            r.DateFin > start)
                .Select(r => new { r.DateDebut, r.DateFin })
                .ToListAsync();

            // 3. Calculate daily occupancy
            var unavailableDates = new List<string>();
            
            for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
            {
                int bookedCount = reservations.Count(r => r.DateDebut <= date && r.DateFin > date);
                if (bookedCount >= totalRooms)
                {
                    unavailableDates.Add(date.ToString("yyyy-MM-dd"));
                }
            }

            return Ok(unavailableDates);
        }

        // POST: api/Reservations
        [HttpPost]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> CreateReservation([FromBody] ReservationRequestDto request)
        {
            if (request.StartDate >= request.EndDate)
                return BadRequest("Dates invalides.");

            // Get current user's ClientId
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var client = await _context.Clients.Include(c => c.ApplicationUser).FirstOrDefaultAsync(c => c.ApplicationUserId == userId);
            
            if (client == null)
                return BadRequest("Profil client introuvable.");

            // Re-check availability
            var bookedRoomIds = await _context.ReservationChambres
                .Where(r => r.Chambre.TypeChambreId == request.TypeId && 
                            r.Statut != "Annulée" &&
                            r.DateDebut < request.EndDate && 
                            r.DateFin > request.StartDate)
                .Select(r => r.ChambreId)
                .Distinct()
                .ToListAsync();

            var room = await _context.Chambres
                .Where(c => c.TypeChambreId == request.TypeId && !bookedRoomIds.Contains(c.Id))
                .FirstOrDefaultAsync();

            if (room == null)
                return BadRequest("Désolé, plus de chambres disponibles pour ces dates.");

            // Calculate Price
            var type = await _context.TypeChambres.FindAsync(request.TypeId);
            var days = (request.EndDate - request.StartDate).Days;
            if (days == 0) days = 1; // Minimum 1 night
            var totalPrice = type.Tarif * days;

            // Create Reservation
            var reservation = new ReservationChambre
            {
                DateDebut = request.StartDate,
                DateFin = request.EndDate,
                Statut = "Confirmée",
                PrixTotal = totalPrice,
                DateCreation = DateTime.Now,
                ClientId = client.Id,
                ChambreId = room.Id
            };

            _context.ReservationChambres.Add(reservation);
            
            // Update room state
            room.Etat = "Réservée";
            
            // Award points logic moved to checkout (1 point / 20 eur)
            // client.PointsFidelite += 10;

            await _context.SaveChangesAsync();

            // SEND EMAIL
            try
            {
                var sb = new StringBuilder();
                sb.Append($"<h1>Confirmation de Réservation</h1>");
                sb.Append($"<p>Bonjour {client.Nom} {client.Prenom},</p>");
                sb.Append($"<p>Votre réservation de chambre a été confirmée.</p>");
                sb.Append($"<p><strong>Détails :</strong></p>");
                sb.Append($"<ul>");
                sb.Append($"<li><strong>Type :</strong> {room.TypeChambre?.Nom}</li>");
                sb.Append($"<li><strong>Chambre :</strong> N°{room.Numero}</li>");
                sb.Append($"<li><strong>Dates :</strong> {request.StartDate:dd/MM/yyyy} au {request.EndDate:dd/MM/yyyy}</li>");
                sb.Append($"<li><strong>Prix Total :</strong> {totalPrice} €</li>");
                sb.Append($"</ul>");
                
                sb.Append("<p>Cordialement,<br>L'équipe Blue Kasbah Resort</p>");

                if (client.ApplicationUser != null)
                {
                    await _emailService.SendEmailAsync(client.ApplicationUser.Email, "Confirmation de Réservation - Blue Kasbah", sb.ToString());
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error sending email: {ex.Message}");
            }

            return Ok(new { message = "Réservation confirmée !", reservationId = reservation.Id });
        }

        // GET: api/Reservations/my-reservations
        [HttpGet("my-reservations")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> GetMyReservations()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.ApplicationUserId == userId);
            
            if (client == null) return BadRequest("Client introuvable.");

            var reservations = await _context.ReservationChambres
                .Include(r => r.Chambre)
                .ThenInclude(c => c.TypeChambre)
                .Where(r => r.ClientId == client.Id && r.Statut == "Confirmée")
                .OrderByDescending(r => r.DateDebut)
                .Select(r => new 
                {
                    r.Id,
                    r.DateDebut,
                    r.DateFin,
                    r.PrixTotal,
                    r.Statut,
                    ChambreNumero = r.Chambre.Numero,
                    TypeChambre = r.Chambre.TypeChambre.Nom,
                    Image = r.Chambre.TypeChambre.ImagePath
                })
                .ToListAsync();

            return Ok(reservations);
        }

        // GET: api/Reservations/all
        [HttpGet("all")]
        [Authorize(Roles = "Responsable")]
        public async Task<IActionResult> GetAllReservations()
        {
            // Check if user is Responsable Chambre or Admin
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var responsable = await _context.Responsables.FirstOrDefaultAsync(r => r.ApplicationUserId == userId);

            if (responsable == null) return Unauthorized();

            if (!responsable.IsResponsableChambre && !responsable.Role.EndsWith("/ADMIN"))
            {
                return StatusCode(403, "Accès réservé aux responsables des chambres.");
            }

            var reservations = await _context.ReservationChambres
                .Include(r => r.Chambre)
                .ThenInclude(c => c.TypeChambre)
                .Include(r => r.Client)
                .ThenInclude(c => c.ApplicationUser)
                .OrderByDescending(r => r.DateDebut)
                .Select(r => new 
                {
                    r.Id,
                    r.DateDebut,
                    r.DateFin,
                    r.PrixTotal,
                    r.Statut,
                    ChambreNumero = r.Chambre.Numero,
                    TypeChambre = r.Chambre.TypeChambre.Nom,
                    ClientNom = r.Client != null ? r.Client.Nom + " " + r.Client.Prenom : (r.NomClientNonInscrit ?? "Inconnu"),
                    ClientEmail = r.Client != null ? r.Client.ApplicationUser.Email : "N/A"
                })
                .ToListAsync();

            return Ok(reservations);
        }

        // POST: api/Reservations/create-admin
        [HttpPost("create-admin")]
        [Authorize(Roles = "Responsable")]
        public async Task<IActionResult> CreateAdminReservation([FromBody] AdminReservationRequestDto request)
        {
            if (request.StartDate >= request.EndDate)
                return BadRequest("Dates invalides.");

            // Check permissions
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var responsable = await _context.Responsables.FirstOrDefaultAsync(r => r.ApplicationUserId == userId);
            if (responsable == null || (!responsable.IsResponsableChambre && !responsable.Role.EndsWith("/ADMIN")))
                return StatusCode(403, "Accès réservé aux responsables des chambres.");

            // Re-check availability
            var bookedRoomIds = await _context.ReservationChambres
                .Where(r => r.Chambre.TypeChambreId == request.TypeId && 
                            r.Statut != "Annulée" &&
                            r.DateDebut < request.EndDate && 
                            r.DateFin > request.StartDate)
                .Select(r => r.ChambreId)
                .Distinct()
                .ToListAsync();

            var room = await _context.Chambres
                .Where(c => c.TypeChambreId == request.TypeId && !bookedRoomIds.Contains(c.Id))
                .FirstOrDefaultAsync();

            if (room == null)
                return BadRequest("Plus de chambres disponibles.");

            // Calculate Price
            var type = await _context.TypeChambres.FindAsync(request.TypeId);
            var days = (request.EndDate - request.StartDate).Days;
            if (days == 0) days = 1; 
            var totalPrice = type.Tarif * days;

            var reservation = new ReservationChambre
            {
                DateDebut = request.StartDate,
                DateFin = request.EndDate,
                Statut = "Confirmée",
                PrixTotal = totalPrice,
                DateCreation = DateTime.Now,
                ChambreId = room.Id
            };

            if (request.ClientId.HasValue)
            {
                reservation.ClientId = request.ClientId.Value;
            }
            else if (!string.IsNullOrEmpty(request.NomClientNonInscrit))
            {
                reservation.NomClientNonInscrit = request.NomClientNonInscrit;
            }
            
            _context.ReservationChambres.Add(reservation);
            room.Etat = "Réservée";
            
            await _context.SaveChangesAsync();

            return Ok(new { message = "Réservation créée !", reservationId = reservation.Id });
        }

        // GET: api/Reservations/clients
        [HttpGet("clients")]
        [Authorize(Roles = "Responsable")]
        public async Task<IActionResult> GetClients()
        {
            var clients = await _context.Clients
                .Include(c => c.ApplicationUser)
                .Select(c => new 
                {
                    c.Id,
                    NomComplet = c.Nom + " " + c.Prenom,
                    Email = c.ApplicationUser.Email
                })
                .ToListAsync();
            return Ok(clients);
        }
        [HttpPost("complete/{id}")]
        [Authorize(Roles = "Responsable")]
        public async Task<IActionResult> CompleteReservation(int id)
        {
            var reservation = await _context.ReservationChambres
                .Include(r => r.Chambre)
                .Include(r => r.Client)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null) return NotFound("Réservation introuvable.");

            if (reservation.Statut == "Terminée")
                return BadRequest("La réservation est déjà terminée.");

            // 1. Update Reservation Status
            reservation.Statut = "Terminée";

            // 2. Free the room
            reservation.Chambre.Etat = "Disponible";

            // 3. Free the room
            reservation.Chambre.Etat = "Disponible";

            // Points are now awarded at creation time, so we don't award them here anymore.
            // But we keep the method to free the room and close the reservation.

            await _context.SaveChangesAsync();

            return Ok(new { message = "Réservation terminée !" });
        }
    }

    public class ReservationRequestDto
    {
        public int TypeId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int? ClientId { get; set; } // Optional for admin override
    }

    public class AdminReservationRequestDto
    {
        public int TypeId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int? ClientId { get; set; }
        public string? NomClientNonInscrit { get; set; }
    }
}
