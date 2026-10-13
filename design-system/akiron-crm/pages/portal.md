# Client portal (`/portal/*`)

Overrides MASTER.md where they differ. The reader is the agency's **client**: they come a few times
a month, often on a phone, to answer three questions: where is my work, what do I owe, what do you
need from me. They do not know the product and will not learn it.

## Layout

- **No sidebar.** A 56px top bar: the agency's name on the left (the portal is theirs, not ours),
  four destinations in the middle (Özet · İşler · Teklifler · Hesap), the person's name and sign-out
  on the right. Below 768px the destinations move to a bottom tab bar with icon + label, 56px tall,
  44px targets.
- Content column max **960px**, centred; one column. No dense tables on phones: lists of rows that
  stack (title, status badge, one meta line, amount right-aligned).
- "Akiron" appears once, small, in the footer.

## Content rules

- Plain words, no internal vocabulary: "İşleriniz", "Ödenecek", "Mesaj yazın"; never "cari",
  "açık kalem", "iş emri".
- Status is a badge with a label (never colour alone) plus, for work, a progress bar when the job
  has a checklist. Stage names are the agency's own.
- Money due is the one number that may be large (22/28); overdue uses `destructive` with the word
  "gecikti", upcoming stays neutral.
- The thread is a conversation: the client's messages right-aligned on `primary-soft`, the team's
  left on `muted`, name and time under each; file events are one-line rows with a download link.
- Every list has an empty state that says what will appear there; no invented numbers.

## Sign-in

One field (e-mail) and one button; after sending, the form is replaced by "check your inbox" with
the address shown. No password, no register link. The link page shows a spinner, then either the
portal or one sentence and a way to ask for a new link.

## Decisions

| Considered | Chosen | Why |
| --- | --- | --- |
| Reuse the staff shell with fewer menu items | Separate top-bar shell | Clients must never see staff chrome; four destinations do not need a sidebar, and a top/bottom bar reads as "a site", which is what they expect. |
| Kanban of the client's jobs | Stacked list with badge + progress | A board implies they can move cards; they cannot. |
| Tables for the account | Stacked rows, statement behind a disclosure | Most visits are on a phone and ask one question: what is due. |
