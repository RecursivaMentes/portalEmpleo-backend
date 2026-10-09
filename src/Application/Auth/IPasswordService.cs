using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Auth
{
    public interface IPasswordService
    {
        string HashPassword(string password);

        bool verifyPassword(string hashedPassword, string password);
    }
}
