using System.Net;

namespace Vita.Planning.Application.Exceptions;

public sealed class EconomicApprovalException : InvalidOperationException
{
    public string Title { get; }
    public string ErrorCode { get; }
    public HttpStatusCode StatusCode { get; }

    public EconomicApprovalException(string title, string detail, string errorCode, HttpStatusCode statusCode = HttpStatusCode.Conflict)
        : base(detail)
    {
        Title = title;
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }
}
