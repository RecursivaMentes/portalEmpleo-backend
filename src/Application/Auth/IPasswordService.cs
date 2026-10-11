using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Auth
{
    public interface IPasswordService
    {
        string HashPassword(string password);

        bool VerifyPassword(string hashedPassword, string password);
    }
}
