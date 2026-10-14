using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Auth
{
    public record TokenResult(
        string AccessToken,
        DateTime ExpiresAtUtc
        ); 
}
