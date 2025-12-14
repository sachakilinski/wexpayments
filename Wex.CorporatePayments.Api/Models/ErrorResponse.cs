namespace Wex.CorporatePayments.Api.Models;

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public object? Details { get; set; }
}

public class ValidationErrorDetail
{
    public string Property { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public object? AttemptedValue { get; set; }
}
