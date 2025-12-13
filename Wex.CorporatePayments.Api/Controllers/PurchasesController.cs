using Microsoft.AspNetCore.Mvc;
using MediatR;
using Wex.CorporatePayments.Application.Commands;
using Wex.CorporatePayments.Application.Exceptions;
using Wex.CorporatePayments.Application.Queries;
using Wex.CorporatePayments.Application.UseCases;
using FluentValidation;

namespace Wex.CorporatePayments.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PurchasesController : ControllerBase
{
    private readonly IStorePurchaseTransactionUseCase _storePurchaseTransactionUseCase;
    private readonly IMediator _mediator;

    public PurchasesController(
        IStorePurchaseTransactionUseCase storePurchaseTransactionUseCase,
        IMediator mediator)
    {
        _storePurchaseTransactionUseCase = storePurchaseTransactionUseCase;
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreatePurchase([FromBody] StorePurchaseCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var purchaseId = await _storePurchaseTransactionUseCase.HandleAsync(command, cancellationToken);
            return CreatedAtAction(nameof(GetPurchase), new { id = purchaseId }, new { Id = purchaseId });
        }
        catch (ValidationException ex)
        {
            // Return 400 Bad Request with validation errors
            var errors = ex.Errors.Select(e => new 
            {
                Property = e.PropertyName,
                ErrorMessage = e.ErrorMessage,
                AttemptedValue = e.AttemptedValue
            });
            
            return BadRequest(new { 
                Error = "Validation failed",
                Code = "VALIDATION_ERROR",
                Errors = errors
            });
        }
        catch (IdempotencyConflictException ex)
        {
            // Return 409 Conflict for duplicate idempotency key
            return Conflict(new { 
                Error = ex.Message, 
                IdempotencyKey = ex.IdempotencyKey,
                Code = "IDEMPOTENCY_CONFLICT"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetPurchase(Guid id, [FromQuery] string currency = "BRL", CancellationToken cancellationToken = default)
    {
        try
        {
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
            return NotFound(new { Error = ex.Message, Code = "PURCHASE_NOT_FOUND" });
        }
        catch (ExchangeRateUnavailableException ex)
        {
            return BadRequest(new { Error = ex.Message, Code = "EXCHANGE_RATE_UNAVAILABLE" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message, Code = "UNEXPECTED_ERROR" });
        }
    }
}
