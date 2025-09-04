using Microsoft.AspNetCore.Mvc;
using SmallShopBigAmbitions.Application._Abstractions;
using SmallShopBigAmbitions.Application.Billing.ChargeCustomer;
using SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay;
using SmallShopBigAmbitions.Application.Cart.GetCartForUser;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using System.Security.Cryptography;
using System.Text;

namespace SmallShopBigAmbitions.Business.Controllers;

public record ChargeRequestDto(Guid CartId, Guid CustomerId);



[ApiController]
[Route("api/[controller]")]
public class BillingController(IFunctionalDispatcher dispatcher, ICartService cartService) : ControllerBase
{
    private readonly IFunctionalDispatcher _dispatcher = dispatcher;
    private readonly ICartService _cartService = cartService;

    [HttpPost("charge")]
    public async Task<IActionResult> ChargeCustomer([FromBody] ChargeRequestDto request)
    {
        var trustedContext = TrustedContextFactory.FromHttpContext(HttpContext);
        var ct = HttpContext.RequestAborted;
        // need to manage a Cart for starters:
        var cart = _cartService.GetCartByUserId(request.CustomerId);
        var command = new ChargeCustomerCommand(request.CustomerId, request.CartId, cart);
        var result = await _dispatcher.Dispatch(command, ct).RunAsync();

        return result.Match<IActionResult>(
            Succ: r => Ok(r),
            Fail: err => Unauthorized(new { error = err.Message })
        );
    }

    [HttpGet("payment_intent")]
    public async Task<IActionResult> CreatePaymentIntent([FromQuery] PaymentIntentDTOPayloadFromView payload, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        var trustedContext = TrustedContextFactory.FromHttpContext(HttpContext);
        var ct = HttpContext.RequestAborted;

        // Prefer header if provided
        var effectivePayload = string.IsNullOrWhiteSpace(payload.IdempotencyKey) && !string.IsNullOrWhiteSpace(idempotencyKey)
            ? payload with { IdempotencyKey = idempotencyKey }
            : payload;

        var query = CreateIntentToPayCommand(effectivePayload);
        var result = await _dispatcher.Dispatch(query, ct).RunAsync();
        return result.Match<IActionResult>(
            Succ: intent => Ok(intent),
            Fail: err => BadRequest(new { error = err.Message })
        );
    }

    private static IntentToPayCommand CreateIntentToPayCommand(PaymentIntentDTOPayloadFromView payload)
    {
        return new IntentToPayCommand(
            CartId: payload.CartId,
            Method: payload.Method ?? PaymentMethod.Card,
            Currency: payload.Currency,
            IdempotencyKey: payload.IdempotencyKey ?? GenerateIdempotencyKey(payload),
            ShippingAddress: payload.ShippingAddress,
            Metadata: ToMap(payload.Metadata)
        );
    }

    private static Map<string, string> ToMap(Dictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
            return Map<string, string>();

        return metadata.Aggregate(Map<string, string>(), (acc, kv) => acc.Add(kv.Key, kv.Value));
    }

    private static string GenerateIdempotencyKey(PaymentIntentDTOPayloadFromView payload)
    {
        // Prefer client-supplied keys for true idempotency across retries.
        // If not provided, derive a deterministic key from stable fields so repeated identical requests dedupe.
        var raw = $"intent-create|{payload.UserId}|{payload.CartId}|{payload.Currency}|{payload.Method ?? PaymentMethod.Card}|{payload.ShippingAddress}";
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    public record PaymentIntentDTOPayloadFromView()
    {
        public Guid UserId { get; init; }
        public Guid CartId { get; init; }
        public string Currency { get; init; } = "SEK";
        public string? ShippingAddress { get; init; }
        public PaymentMethod? Method { get; init; }
        public string? IdempotencyKey { get; init; }
        public Dictionary<string, string>? Metadata { get; init; }
    }
}