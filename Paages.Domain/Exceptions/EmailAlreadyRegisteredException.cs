namespace Paages.Domain.Exceptions;

public class EmailAlreadyRegisteredException : Exception
{
    public EmailAlreadyRegisteredException() : base("Email is already registered.") { }
}