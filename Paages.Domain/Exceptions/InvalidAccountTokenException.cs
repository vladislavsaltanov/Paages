namespace Paages.Domain.Exceptions;

public class InvalidAccountTokenException : Exception
{
    public InvalidAccountTokenException() : base("Invalid, expired, or already used token.") { }
}
