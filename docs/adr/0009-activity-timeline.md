# ADR-0009: Activity timeline as a first-class read model

Status: Accepted · Date: 2026-09-29

## Context

The screen users will spend most time on is a party's (or a job's) history in one stream:

```text
ABC Mobilya
29 Sep  15:42  Payment received            +25.000 TL
        14:30  Quote #104 approved
        13:11  Customer viewed quote #104
28 Sep  17:42  WhatsApp message received
        16:35  Revision 3 uploaded to job #42
27 Sep  12:20  Work order #42 created
```

Every module contributes to it, it has to render in Turkish and English, the client portal shows a filtered version of it, and later AI summaries read it. Treated as a log table bolted on at the end, it becomes an unqueryable pile of strings. Treated as a first-class read model from the start, it is cheap.

## Decision

**Shape.** A platform-level `Timeline` owned by BuildingBlocks' contracts and stored in its own schema (`timeline`):

- `TimelineEntry`: id (GUIDv7, so ids sort by time), tenant, **type** (stable key such as `sales.quote.approved`, `finance.collection.received`, `messaging.whatsapp.received`, `jobs.revision.uploaded`), `occurredAt`, **actor** (user, portal contact, system, or integration, with id and display name snapshot), **payload** (`jsonb`: the few values the line needs to render, e.g. number, amount, currency, file name; snapshots, not live references), **source** (module, entity type, entity id, for the link), **visibility** (`internal` or `portal`), and an idempotency key.
- `TimelineLink`: which streams the entry appears in, as (subject type, subject id) pairs. One payment appears on the party, the invoice and the work order; one WhatsApp message on the party and, once assigned, the job. Queries go through links: index on `(tenant_id, subject_type, subject_id, occurred_at desc)`.

**Writing.**

- Modules never insert into the timeline tables. They raise integration events through the outbox (ADR-0001); the timeline projects them. Each event type has one projector that decides the entry type, payload and links. Replaying events rebuilds the timeline.
- User-authored items (note, call log, meeting) are commands of the timeline itself, written directly.
- Idempotency key = source event id, so at-least-once delivery never duplicates a line.
- Entries are immutable. A cancelled collection adds a "collection cancelled" entry; it does not edit or remove the original.

**Reading.**

- `GET /api/v1/timeline/{subjectType}/{subjectId}?before=&types=` with keyset pagination on `(occurred_at, id)`, never offset.
- Rendering is client-side: the web app has a registry from entry type to icon, colour and a dictionary template (`timeline.sales.quote.approved`: "Teklif {number} onaylandı"), filled from the payload. An unknown type falls back to a generic line, so older clients survive new event types.
- The portal endpoint returns only `portal` entries for the signed-in contact's party.
- Permission filtering: entries whose source the user may not see (e.g. finance entries for a member without finance permission) are filtered by the entry type's required permission, declared next to its projector.

**Scope of the first version (Sprint 2).** Tables, projector infrastructure, notes, and the Identity events that exist today (member joined). Each later module adds its projectors in the sprint that builds the module; "writes to the timeline" is part of every module's definition of done.

## Alternatives considered

- **Audit log as timeline** — audit rows are field-level diffs for compliance; the timeline is business events for people. Mixing them makes both worse.
- **Each module renders its own history, merged in the UI** — N queries per screen, no single ordering, no portal filtering.
- **Free-text entries** — cannot be translated, filtered by type or summarised reliably.

## Consequences

- Every integration event needs enough data to render its line without calling back into the module (payload snapshots).
- The timeline is eventually consistent by milliseconds (outbox dispatch); the UI invalidates the timeline query after its own mutations.
- AI features (phase 8) read the timeline through the same API, with the same permission filtering.

## Exit strategy

The timeline is a projection; it can be moved to another store (a separate database, a search index) by replaying events, without changing the modules that emit them.
