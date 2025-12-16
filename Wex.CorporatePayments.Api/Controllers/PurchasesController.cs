using Microsoft.AspNetCore.Mvc;
using MediatR;
using Wex.CorporatePayments.Application.Commands;
using Wex.CorporatePayments.Application.Configuration;
using Wex.CorporatePayments.Application.Exceptions;
using Wex.CorporatePayments.Application.Queries;
using Wex.CorporatePayments.Application.UseCases;
using FluentValidation;
using Wex.CorporatePayments.Api.Models;

namespace Wex.CorporatePayments.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PurchasesController : ControllerBase
{
    private readonly IStorePurchaseTransactionUseCase _storePurchaseTransactionUseCase;
    private readonly IMediator _mediator;
    private readonly ILogger<PurchasesController> _logger;

    public PurchasesController(
        IStorePurchaseTransactionUseCase storePurchaseTransactionUseCase,
        IMediator mediator,
        ILogger<PurchasesController> logger)
    {
        _storePurchaseTransactionUseCase = storePurchaseTransactionUseCase;
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreatePurchase([FromBody] StorePurchaseCommand command, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var purchaseId = await _storePurchaseTransactionUseCase.HandleAsync(command, idempotencyKey, cancellationToken);
            return CreatedAtAction(nameof(GetPurchase), new { id = purchaseId }, new { Id = purchaseId });
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Validation failed for purchase creation with {@Errors}", ex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage, e.AttemptedValue }));
            
            var errorDetails = ex.Errors.Select(e => new ValidationErrorDetail
            {
                Property = e.PropertyName,
                Message = e.ErrorMessage,
                AttemptedValue = e.AttemptedValue
            }).ToList();
            
            return BadRequest(new ErrorResponse 
            { 
                Error = "Validation failed",
                Code = ApplicationConstants.ErrorCodes.ValidationFailed,
                Details = errorDetails
            });
        }
        catch (IdempotencyConflictException ex)
        {
            _logger.LogWarning("Idempotency conflict detected for key {IdempotencyKey}", ex.IdempotencyKey);
            
            // Return 409 Conflict for duplicate idempotency key
            return Conflict(new ErrorResponse 
            { 
                Error = ex.Message, 
                Code = ApplicationConstants.ErrorCodes.IdempotencyConflict,
                Details = new { IdempotencyKey = ex.IdempotencyKey }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during purchase creation");
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetPurchase(Guid id, [FromQuery] string currency = ApplicationConstants.Currency.Default, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Retrieving purchase {PurchaseId} with currency conversion to {TargetCurrency}", id, currency);
            
            var query = new RetrieveConvertedPurchaseQuery(id, currency);
            var result = await _mediator.Send(query, cancellationToken);
            
            return Ok(new
            {
                Id = result.Id,
                Description = result.Description,
                TransactionDate = result.TransactionDate,
                OriginalAmount = new
                {
                    Value = result.OriginalAmount.Amount,
                    Currency = result.OriginalAmount.Currency
                },
                ConvertedAmount = new
                {
                    Value = result.ConvertedAmount.Amount,
                    Currency = result.ConvertedAmount.Currency
                },
                ExchangeRate = result.ExchangeRate,
                ExchangeRateDate = result.ExchangeRateDate
            });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Purchase not found: {PurchaseId}", id);
            return NotFound(new ErrorResponse { Error = ex.Message, Code = ApplicationConstants.ErrorCodes.PurchaseNotFound });
        }
        catch (ExchangeRateUnavailableException ex)
        {
            _logger.LogWarning("Exchange rate unavailable for currency {Currency} on date {Date}", currency, ex.Date);
            return BadRequest(new ErrorResponse { Error = ex.Message, Code = ApplicationConstants.ErrorCodes.ExchangeRateUnavailable });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving purchase {PurchaseId}", id);
            return BadRequest(new ErrorResponse { Error = ex.Message, Code = ApplicationConstants.ErrorCodes.UnexpectedError });
        }
    }
}
