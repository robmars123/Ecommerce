namespace Ecommerce.SharedKernel.Domain;

public sealed class DomainException(string message) : Exception(message);
