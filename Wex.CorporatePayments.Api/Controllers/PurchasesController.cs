using Microsoft.AspNetCore.Mvc;
using Wex.CorporatePayments.Application.Commands;
using Wex.CorporatePayments.Application.UseCases;

namespace Wex.CorporatePayments.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PurchasesController : ControllerBase
{
    private readonly IStorePurchaseTransactionUseCase _storePurchaseTransactionUseCase;

    public PurchasesController(IStorePurchaseTransactionUseCase storePurchaseTransactionUseCase)
    {
        _storePurchaseTransactionUseCase = storePurchaseTransactionUseCase;
    }

    [HttpPost]
    public async Task<IActionResult> CreatePurchase([FromBody] StorePurchaseCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var purchaseId = await _storePurchaseTransactionUseCase.HandleAsync(command, cancellationToken);
            return CreatedAtAction(nameof(GetPurchase), new { id = purchaseId }, new { Id = purchaseId });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetPurchase(Guid id, CancellationToken cancellationToken = default)
    {
        // This would require implementing GetById in the use case
        // For now, return a placeholder response
        return Ok(new { Id = id, Message = "Purchase retrieval not implemented yet" });
    }
}
