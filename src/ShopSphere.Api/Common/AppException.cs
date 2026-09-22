namespace ShopSphere.Api.Common;

public abstract class AppException : Exception
{
    protected AppException(string title, string message, int statusCode) : base(message)
    {
        Title = title;
        StatusCode = statusCode;
    }

    public string Title { get; }
    public int StatusCode { get; }
}

public class NotFoundException : AppException
{
    public NotFoundException(string message)
        : base("Resource not found", message, StatusCodes.Status404NotFound)
    {
    }
}

public class BusinessRuleException : AppException
{
    public BusinessRuleException(string title, string message, int statusCode = StatusCodes.Status409Conflict)
        : base(title, message, statusCode)
    {
    }
}
