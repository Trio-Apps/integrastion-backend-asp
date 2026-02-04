using System;
using System.Threading;
using System.Threading.Tasks;

namespace OrderXChange.Application.Contracts.Integrations.Talabat;

/// <summary>
/// Interface for managing Talabat catalog sync status.
/// Handles updates from webhooks and status tracking.
/// </summary>
public interface ITalabatSyncStatusService
{
	/// <summary>
	/// Updates sync log and staging products when catalog import completes successfully
	/// </summary>
	Task HandleImportCompletedAsync(
		TalabatCatalogStatusWebhook webhook,
		string rawPayload,
		string correlationId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Updates sync log and staging products when catalog import fails
	/// </summary>
	Task HandleImportFailedAsync(
		TalabatCatalogStatusWebhook webhook,
		string rawPayload,
		string correlationId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Updates sync log and staging products when catalog import partially succeeds
	/// </summary>
	Task HandleImportPartialAsync(
		TalabatCatalogStatusWebhook webhook,
		string rawPayload,
		string correlationId,
		CancellationToken cancellationToken = default);
}

