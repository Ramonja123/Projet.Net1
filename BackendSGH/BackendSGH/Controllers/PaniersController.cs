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
    [Authorize(Roles = "Client")]
    public class PaniersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService; // Added

        public PaniersController(ApplicationDbContext context, IConfiguration configuration, IEmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
        }

        // GET: api/Paniers/active
        [HttpGet("active")]
        public async Task<IActionResult> GetActivePanier()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.ApplicationUserId == userId);
            if (client == null) return BadRequest("Client introuvable.");

            var panier = await _context.Paniers
                .Include(p => p.ReservationChambres)
                .ThenInclude(r => r.Chambre)
                .ThenInclude(c => c.TypeChambre)
                .Include(p => p.ReservationServices)
                .ThenInclude(r => r.Service)
                .FirstOrDefaultAsync(p => p.ClientId == client.Id && p.Statut == "Actif");

            if (panier == null) return Ok(null);

            // Recalculate total
            decimal realTotal = 0;
            if (panier.ReservationChambres != null)
            {
                foreach(var r in panier.ReservationChambres) realTotal += r.PrixTotal;
            }
            if (panier.ReservationServices != null)
            {
                foreach(var r in panier.ReservationServices) realTotal += (r.Prix ?? 0);
            }
            
            if (panier.Total != realTotal)
            {
                panier.Total = realTotal;
                await _context.SaveChangesAsync();
            }

            return Ok(panier);
        }

        // POST: api/Paniers/add
        [HttpPost("add")]
        public async Task<IActionResult> AddToPanier([FromBody] PanierRequestDto request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.ApplicationUserId == userId);
            if (client == null) return BadRequest("Client introuvable.");

            // 1. Get or Create Active Panier
            var panier = await _context.Paniers
                .Include(p => p.ReservationChambres)
                .FirstOrDefaultAsync(p => p.ClientId == client.Id && p.Statut == "Actif");

            if (panier == null)
            {
                panier = new Panier
                {
                    ClientId = client.Id,
                    Statut = "Actif",
                    Total = 0,
                    ReservationChambres = new List<ReservationChambre>()
                };
                _context.Paniers.Add(panier);
                await _context.SaveChangesAsync();
            }

            // 2. Find Available Room
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

            if (room == null) return BadRequest("Plus de disponibilité pour ces dates.");

            // 3. Calculate Price
            var type = await _context.TypeChambres.FindAsync(request.TypeId);
            var days = (request.EndDate - request.StartDate).Days;
            if (days == 0) days = 1;
            var price = type.Tarif * days;

            // 4. Create Reservation (In Cart)
            var reservation = new ReservationChambre
            {
                DateDebut = request.StartDate,
                DateFin = request.EndDate,
                Statut = "Panier", // Temporary status
                PrixTotal = price,
                DateCreation = DateTime.Now,
                ClientId = client.Id,
                ChambreId = room.Id,
                PanierId = panier.Id
            };

            _context.ReservationChambres.Add(reservation);
            
            // 5. Update Panier Total
            panier.Total += price;
            
            await _context.SaveChangesAsync();

            return Ok(new { message = "Ajouté au panier !", panierId = panier.Id });
        }

        // DELETE: api/Paniers/remove/{id}
        [HttpDelete("remove/{id}")]
        public async Task<IActionResult> RemoveItem(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.ApplicationUserId == userId);
            if (client == null) return BadRequest("Client introuvable.");

            var reservation = await _context.ReservationChambres
                .Include(r => r.Panier)
                .FirstOrDefaultAsync(r => r.Id == id && r.ClientId == client.Id && r.Panier.Statut == "Actif");

            if (reservation == null) return NotFound("Article non trouvé.");

            var panier = reservation.Panier;
            panier.Total -= reservation.PrixTotal;
            if (panier.Total < 0) panier.Total = 0;

            _context.ReservationChambres.Remove(reservation);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Article supprimé.", newTotal = panier.Total });
        }

        // DELETE: api/Paniers/remove-service/{id}
        [HttpDelete("remove-service/{id}")]
        public async Task<IActionResult> RemoveServiceItem(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.ApplicationUserId == userId);
            if (client == null) return BadRequest("Client introuvable.");

            var reservation = await _context.ReservationServices
                .Include(r => r.Panier)
                .FirstOrDefaultAsync(r => r.Id == id && r.ClientId == client.Id && r.Panier.Statut == "Actif");

            if (reservation == null) return NotFound("Service non trouvé dans le panier.");

            var panier = reservation.Panier;
            panier.Total -= (reservation.Prix ?? 0);
            if (panier.Total < 0) panier.Total = 0;

            _context.ReservationServices.Remove(reservation);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Service supprimé.", newTotal = panier.Total });
        }

        // POST: api/Paniers/checkout
        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout([FromQuery] int pointsUsed = 0)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var client = await _context.Clients.Include(c => c.ApplicationUser).FirstOrDefaultAsync(c => c.ApplicationUserId == userId);
            if (client == null) return BadRequest("Client introuvable.");

            var panier = await _context.Paniers
                .Include(p => p.ReservationChambres)
                .Include(p => p.ReservationServices)
                .FirstOrDefaultAsync(p => p.ClientId == client.Id && p.Statut == "Actif");

            if (panier == null || (!panier.ReservationChambres.Any() && !panier.ReservationServices.Any())) 
                return BadRequest("Panier vide.");

            // Deduct Points
            if (pointsUsed > 0)
            {
                if (client.PointsFidelite < pointsUsed) return BadRequest("Triche détectée: Points insuffisants.");
                client.PointsFidelite -= pointsUsed;
            }

            // Confirm Rooms
            foreach (var res in panier.ReservationChambres)
            {
                res.Statut = "Confirmée";
                var room = await _context.Chambres.FindAsync(res.ChambreId);
                if (room != null) room.Etat = "Réservée";
            }

            // Confirm Services
            foreach (var res in panier.ReservationServices)
            {
                res.Statut = "Confirmée";
            }

            // Calculate Earned Points (based on the original Total, not discounted one, is fairer? Or discounted? Usually discounted.)
            // Let's use the discounted amount actually paid.
            // Panier.Total is currently the sum of items. 
            decimal amountPaid = Math.Max(0, panier.Total - pointsUsed); // 1 pt = 1 eur
            
            int pointsEarned = (int)(amountPaid / 20); // 1 point per 20 eur spent
            
            if (pointsEarned > 0)
            {
                client.PointsFidelite += pointsEarned;
            }

            panier.Statut = "Payé"; 
            
            await _context.SaveChangesAsync();

            // SEND EMAIL
            try 
            {
                var sb = new StringBuilder();
                sb.Append($"<h1>Confirmation de Paiement</h1>");
                sb.Append($"<p>Bonjour {client.Nom} {client.Prenom},</p>");
                sb.Append($"<p>Merci pour votre paiement. Voici le détail de vos réservations confirmées :</p>");
                sb.Append("<table border='1' cellpadding='5' cellspacing='0' style='border-collapse: collapse;'>");
                sb.Append("<tr style='background-color: #f2f2f2;'><th>Type</th><th>Détail</th><th>Prix</th></tr>");

                foreach (var res in panier.ReservationChambres)
                {
                    sb.Append($"<tr><td>Chambre</td><td>{res.Chambre?.TypeChambre?.Nom} (N°{res.Chambre?.Numero})<br>{res.DateDebut:dd/MM/yyyy} - {res.DateFin:dd/MM/yyyy}</td><td>{res.PrixTotal} €</td></tr>");
                }
                foreach (var res in panier.ReservationServices)
                {
                    sb.Append($"<tr><td>Service</td><td>{res.Service?.Nom}<br>{res.Date:dd/MM/yyyy} {res.Heure}</td><td>{res.Prix ?? 0} €</td></tr>");
                }
                
                sb.Append($"<tr><td colspan='2'><strong>Sous-Total</strong></td><td><strong>{panier.Total} €</strong></td></tr>");
                if(pointsUsed > 0)
                {
                     sb.Append($"<tr><td colspan='2' style='color:green;'>Points Fidélité Utilisés (-{pointsUsed}€)</td><td style='color:green;'>-{pointsUsed} €</td></tr>");
                }
                sb.Append($"<tr style='background-color: #e6f7ff;'><td colspan='2'><strong>TOTAL PAYÉ</strong></td><td><strong>{amountPaid} €</strong></td></tr>");
                sb.Append("</table>");
                
                if (pointsEarned > 0)
                {
                    sb.Append($"<p><strong>Félicitations !</strong> Vous avez gagné <strong>{pointsEarned} points</strong> de fidélité avec cet achat.</p>");
                }

                sb.Append("<p>Cordialement,<br>L'équipe Blue Kasbah Resort</p>");

                await _emailService.SendEmailAsync(client.ApplicationUser.Email, "Confirmation de Réservation - Blue Kasbah", sb.ToString());
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error sending email: {ex.Message}");
            }

            return Ok(new { message = $"Paiement réussi ! Points utilisés : {pointsUsed}. Vous avez gagné {pointsEarned} nouveaux points." });
        }

        [HttpPost("create-checkout-session")]
        public async Task<IActionResult> CreateCheckoutSession([FromQuery] int pointsUsed = 0)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.ApplicationUserId == userId);
            if (client == null) return BadRequest("Client introuvable.");

            // 1. Validate Points
            if (pointsUsed < 0) return BadRequest("Points invalides.");
            if (pointsUsed > client.PointsFidelite) return BadRequest("Solde de points insuffisant.");

            var panier = await _context.Paniers
                .Include(p => p.ReservationChambres)
                .ThenInclude(r => r.Chambre)
                .ThenInclude(c => c.TypeChambre)
                .Include(p => p.ReservationServices)
                .ThenInclude(r => r.Service)
                .FirstOrDefaultAsync(p => p.ClientId == client.Id && p.Statut == "Actif");

            if (panier == null || (!panier.ReservationChambres.Any() && !panier.ReservationServices.Any())) 
                return BadRequest("Panier vide.");

            // 2. Validate Discount Amount vs Total
            // Note: Panier.Total might not always be perfectly up to date if we rely on the DB column, 
            // but the checkout session recalculates line items anyway. We should check against the calculated total.
            
            decimal calculatedTotal = 0;
            foreach(var r in panier.ReservationChambres) calculatedTotal += r.PrixTotal;
            foreach(var r in panier.ReservationServices) calculatedTotal += (r.Prix ?? 0);

            if (pointsUsed > calculatedTotal)
            {
                return BadRequest("Vous ne pouvez pas utiliser plus de points que le montant total.");
            }

            var secretKey = _configuration.GetSection("Stripe:SecretKey").Value;
            if (string.IsNullOrEmpty(secretKey) || secretKey.Contains("YOUR_SECRET_KEY"))
            {
                return StatusCode(500, "Stripe API Key is missing or invalid in appsettings.json.");
            }

            Stripe.StripeConfiguration.ApiKey = secretKey; // Ensure key is set

            var domain = "http://localhost:5002";
            var options = new Stripe.Checkout.SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<Stripe.Checkout.SessionLineItemOptions>(),
                Mode = "payment",
                SuccessUrl = domain + $"/PaymentSuccess?session_id={{CHECKOUT_SESSION_ID}}&pointsUsed={pointsUsed}", // Pass points used to success handler to deduct them later
                CancelUrl = domain + "/Panier",
            };

            foreach (var item in panier.ReservationChambres)
            {
                options.LineItems.Add(new Stripe.Checkout.SessionLineItemOptions
                {
                    PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)(item.PrixTotal * 100), 
                        Currency = "eur",
                        ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.Chambre.TypeChambre.Nom,
                            Description = $"Chambre {item.Chambre.Numero} - {item.DateDebut:dd/MM/yyyy} au {item.DateFin:dd/MM/yyyy}"
                        },
                    },
                    Quantity = 1,
                });
            }

            foreach (var item in panier.ReservationServices)
            {
                options.LineItems.Add(new Stripe.Checkout.SessionLineItemOptions
                {
                    PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)((item.Prix ?? 0) * 100), 
                        Currency = "eur",
                        ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.Service.Nom,
                            Description = $"Service - {item.Date:dd/MM/yyyy} à {item.Heure}"
                        },
                    },
                    Quantity = 1,
                });
            }

            // Apply Discount if points used
            if (pointsUsed > 0)
            {
                var couponService = new Stripe.CouponService();
                var coupon = await couponService.CreateAsync(new Stripe.CouponCreateOptions
                {
                    AmountOff = (long)(pointsUsed * 100), // 1 point = 1 EUR = 100 cents
                    Currency = "eur",
                    Duration = "once",
                    Name = $"Réduction Fidélité ({pointsUsed} points)"
                });

                options.Discounts = new List<Stripe.Checkout.SessionDiscountOptions>
                {
                    new Stripe.Checkout.SessionDiscountOptions { Coupon = coupon.Id }
                };
            }

            var service = new Stripe.Checkout.SessionService();
            Stripe.Checkout.Session session = service.Create(options);

            return Ok(new { id = session.Id });
        }
    }

    public class PanierRequestDto
    {
        public int TypeId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}
