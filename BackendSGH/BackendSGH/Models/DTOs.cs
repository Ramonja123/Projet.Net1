using System;

namespace BackendSGH.Models.Dtos
{
    public class LoginDto
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
    }

    public class RegisterDto
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
        
        public required string Nom { get; set; }     
        public required string Prenom { get; set; }        
        // "Client" ou "Responsable" (Pour Identity)
        public required string UserType { get; set; } 

        // Champs spécifiques Client
        public string? Adresse { get; set; }
        public DateTime? DateNaissance { get; set; }

        // Champs spécifiques Responsable
    
        public string? RoleEmploye { get; set; } 
    }
}