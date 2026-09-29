# ADR-0008: One party model (Cari) for customers, suppliers and both

Status: Accepted · Date: 2026-09-29

## Context

Turkish businesses think in *cari hesap*: one account per counterparty, with one balance, whether the counterparty buys from you, sells to you, or both. Agencies routinely have both kinds with the same company (a printer that is also a client; a freelancer who also refers work). Every Turkish accounting system models it this way: Logo Tiger keeps receivable and payable accounts in a single card table (`LG_CLCARD`, typed by `CARDTYPE`) and posts every module's movements to one ledger (`LG_CLFLINE`). Separate `Customer` and `Supplier` aggregates would split one company's history and balance in two, and every accounting export would have to merge them back.

## Decision

- One aggregate, **`Party`** (UI: *Cari*), in the CRM module. There are no `Customer` or `Supplier` entities anywhere in the codebase; other modules refer to a `PartyId`.
- Roles are flags on the party, not types: `IsCustomer`, `IsSupplier` (both may be true). A party gains a role the first time it is used in that role (first quote → customer; first purchase invoice → supplier); the user can also set it.
- Kind: `Company` or `Person` (şahıs / şahıs şirketi). Tax identity: VKN (10 digits) for companies, TCKN (11 digits) for persons, both checksum-validated; tax office; foreign parties with no Turkish tax id. e-Invoice status (e-Fatura mükellefi or not) is cached from the integrator lookup, not typed by hand.
- Contacts (people at the company), addresses, tags and custom fields hang off the party. A lead becomes a party on conversion; it is not a party before that.
- Money on a party is **computed** from the party ledger (ADR-0004): one balance per currency, split into receivable and payable views, with aging from open items.
- Uniqueness per tenant: tax id when present (partial unique index), otherwise none; duplicates are merged through an explicit merge operation that re-points history.
- Mapping for accounting sync (phase 8): party → Logo `LG_CLCARD` (card type derived from the role flags: customer only, supplier only, or both), party code kept in a per-tenant mapping table, not in the party itself.

## Alternatives considered

- **Separate Customer and Supplier aggregates** — matches naive CRM vocabulary, but duplicates the company, splits its balance and breaks accounting sync.
- **Party with a single `Type` enum (Customer | Supplier | Both)** — close, but "Both" becomes a special case in every query; two independent flags compose.
- **Global parties shared across tenants** — no: a party is a tenant's view of a counterparty, with the tenant's own codes, terms and history.

## Consequences

- Screens say "Müşteriler" and "Tedarikçiler" as filtered views of the same list; the party card shows both sides when both apply.
- Finance, jobs, messaging and the portal depend on `PartyId` only, through the CRM module's contracts.

## Exit strategy

If a real need for role-specific data appears (supplier payment terms, customer credit limits), it goes into role detail records (`CustomerTerms`, `SupplierTerms`) owned by the party, not into new top-level aggregates.
