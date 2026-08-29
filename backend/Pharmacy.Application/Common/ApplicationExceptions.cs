namespace Pharmacy.Application.Common;

public abstract class ApplicationServiceException(string message) : Exception(message);

public sealed class RequestValidationException(string message) : ApplicationServiceException(message);

public sealed class ResourceNotFoundException(string message) : ApplicationServiceException(message);

public sealed class ResourceConflictException(string message) : ApplicationServiceException(message);

public sealed class ForbiddenOperationException(string message) : ApplicationServiceException(message);
