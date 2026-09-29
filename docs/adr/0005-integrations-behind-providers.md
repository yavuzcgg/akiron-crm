# ADR-0005: Integrations behind provider interfaces; encrypted tenant credentials

Status: Accepted · Date: 2026-09-29

## Context

The product depends on external services with long approval lead times and possible vendor changes: PayTR/iyzico, WhatsApp Cloud API, SMS gateways, e-invoice integrators (Nilvera, Uyumsoft, Logo), Meta/LinkedIn publishing, accounting systems (Logo, Bay.t). Each tenant brings its own accounts. akiron-seo stores one encrypted string per provider, which is not enough for providers with several secrets and has no key rotation.

## Decision

- One interface per capability in BuildingBlocks or Contracts: `IPaymentProvider`, `IMessageChannel`, `IEmailSender`, `IEInvoiceProvider`, `IAccountingExporter`, `ISocialPublisher`, `IAdsReportSource`, `IExchangeRateSource`, `IShippingProvider`. Modules depend on the interface; adapters live in `Modules/Integrations/<Provider>/`.
- Every capability ships with a **manual/null adapter** first (e.g. "mark as published", "copy message and send yourself"), so the UI works before the vendor account is approved.
- Tenant credentials are stored in `TenantIntegrationCredential`: provider id, non-secret config (JSON), secret payload (JSON) encrypted with AES-256-GCM, key version, and associated data `TenantId|Provider` bound into the ciphertext. Unique index on `(tenant_id, provider)`. Only Owner/Admin can write; secrets are never returned to the client.
- Master key rotation re-encrypts rows by key version in a background job.
- Inbound webhooks live under `/api/public/webhooks/<provider>/{token}`: signature verification per provider, tenant resolution from the token or provider identifier (ADR-0002), idempotency by provider event id.
- Outbound HTTP clients use `AddStandardResilienceHandler` (timeouts, retries, circuit breaker) and are rate-limited per tenant where the vendor bills per call (WhatsApp, SMS) using the quota ledger pattern from akiron-seo.

## Alternatives considered

- Direct SDK calls from modules — fastest today, unremovable tomorrow.
- A generic "integration platform" (iPaaS) — overkill; the interfaces above are the whole surface.

## Consequences

- Adding a second payment provider (iyzico) is one adapter and one settings screen.
- Sandbox adapters make integration tests possible without vendor accounts.

## Exit strategy

Providers are replaceable by design; the encrypted credential format is versioned so storage can migrate to a secrets manager (Vault, AWS Secrets Manager) without touching modules.
