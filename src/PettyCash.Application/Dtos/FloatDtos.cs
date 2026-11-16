namespace PettyCash.Application.Dtos;

public sealed record FloatSummaryDto(
    decimal Capacity,
    decimal Balance,
    decimal Reserved,
    decimal Available);

public sealed record ReplenishmentRequest(decimal Amount, string? Note);
