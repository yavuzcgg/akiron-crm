# ADR-0011: Client portal — a separate principal, link sign-in, views owned by each module

Status: Accepted · Date: 2026-10-13

## Context

Clients need to see where their work stands, answer quotes, read their account and talk to the team without phoning the agency. ADR-0007 already said portal users are a separate principal that never receives staff permissions; ADR-0009 gave timeline entries a `portal` visibility; ADR-0010 covered one-off public links. What was left open: how a client signs in, where the client-facing endpoints live in a modular monolith (ADR-0001), and how a module that does not own a record (Timeline, Files) knows whose it is.

Clients open the portal a few times a month. A password they set once and forget produces support calls; the people who use it are not ours to train.

## Decision

- **Principal.** `PortalUser` (Portal module, schema `portal`) belongs to one tenant and one party. It is authenticated by its own scheme (`Portal`) and cookie (`akiron_portal`), carrying `tenant_id` and `party_id` and no permission claims. The user id travels as `portal_sub`, not `sub`, so `ICurrentUser.UserId` stays null and a client can never be mistaken for a member of staff in a handler or an audit column. A portal session does not authenticate on staff endpoints and a staff session does not on portal ones; both are tested.
- **Sign-in without a password.** Staff invite a contact; the e-mail carries a single-use link (7 days). Later the client asks for a link by address (20 minutes). Links and sessions are random secrets stored as SHA-256 hashes; a session lives 30 days in a server-side table, so switching a user off ends their sessions at once. "Send me a link" answers the same whether or not the address is known. The link opens a page that posts the token, so mail scanners that fetch URLs do not consume it.
- **Views live with their data.** Each module serves its own client-facing endpoints under `/api/v1/<module>/portal/…`, marked `RequirePortal()`, and scopes every query to `IPortalVisitor.PartyId`: Jobs (work orders: stage, due date, progress — no description, task names, hours or budget), Sales (sent quotes with accept/reject, contracts), Finance (the party's own account), Timeline (the client-visible thread), Files (shared files, uploads). The Portal module owns only accounts, sessions and the agency's portal text. No module reads another's tables and no "portal read model" duplicates them.
- **Whose record is it?** Modules that attach things to records they do not own ask the owner through `IPortalSubject` (Contracts): Jobs answers for `work_order`, CRM for `party`. A record of another party, another tenant, or of no party is "not found" for the client, never "forbidden".
- **Tenant binding.** On portal endpoints the tenant comes from the portal session, even when a staff session is open in the same browser.
- **What crosses the line is explicit.** Notes are internal unless staff tick "visible to client"; files are internal until shared one by one; anything the client writes or uploads is visible to both sides. Each crossing is an event: staff are notified in-app, the client's portal users by e-mail.

## Alternatives considered

- **Portal users as memberships with a "client" role** — one auth stack, but a permission mistake would expose staff data, and every staff query would need a party filter added afterwards.
- **Passwords for clients** — familiar, but adds reset flows, strength rules and breach surface for accounts used monthly. Can be added later as a second factor of choice.
- **A Portal module that reads everything through Contracts interfaces** — a wide interface per module that mirrors its read side, changing in two places for every field shown.
- **JWT for portal sessions** — no table lookup per request, but no instant revocation without a deny-list, which is the same lookup.

## Consequences

- A new client-facing view is an endpoint in the owning module plus a page; it cannot leak another party's rows without failing that module's portal test.
- Every portal request costs one indexed lookup of the session.
- Card payments in the portal wait for a payment provider (PayTR/iyzico); until then the portal shows the agency's payment instructions and takes a receipt upload.

## Exit strategy

The scheme and `IPortalVisitor` are the only seams: replacing link sign-in with passwords, OTP or an external IdP changes the Portal module's `auth` endpoints and handler, nothing in the modules' views.
