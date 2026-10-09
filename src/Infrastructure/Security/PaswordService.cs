using System;
using System.Collections.Generic;
using System.Text;
using Application.Auth;
using Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Security
{
    public class PasswordService : IPasswordService
    {
        private readonly PasswordHasher<Usuario> passwordHasher = new();

        public string HashPassword(string password)
        {
            return passwordHasher.HashPassword(
                new Usuario(),
                password);
        }

        public bool verifyPassword(string passwordHash, string password)
        {
            var result = passwordHasher.VerifyHashedPassword(new Usuario(), passwordHash, password);

            return result != PasswordVerificationResult.Failed;
        }
    }
}
