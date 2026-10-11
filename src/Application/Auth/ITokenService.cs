using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Auth
{
    public interface ITokenService
    {
        TokenResult GenerateToken(Usuario usuario);
    }
}
