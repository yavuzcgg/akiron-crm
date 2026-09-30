import { Archive, Building2, CircleDot, Contact, FileUp, MailPlus, PencilLine, StickyNote, UserPlus, UsersRound, type LucideIcon } from "lucide-react";
import { roleLabel } from "@/features/identity/role-name";
import type { TranslationKey } from "@/lib/i18n";
import type { TranslationParams } from "@/lib/i18n/translate";
import type { TimelineItem } from "./timeline-api";

type Translate = (key: TranslationKey, params?: TranslationParams) => string;
type Payload = Record<string, unknown>;

interface EntryType {
  icon: LucideIcon;
  /** Tailwind classes for the icon badge. */
  tone: string;
  headline: (payload: Payload, actor: string, t: Translate) => string;
  /** Optional longer text under the headline, e.g. a note's content. */
  body?: (payload: Payload) => string | undefined;
  /** Where the entry's record lives, when it has a page. */
  href?: (payload: Payload) => string | undefined;
}

const text = (value: unknown) => (typeof value === "string" ? value : "");

const partyHref = (payload: Payload) => (typeof payload.partyId === "string" ? `/crm/parties/${payload.partyId}` : undefined);

/** Field names the API reports in crm.party.updated, and how people call them. */
const partyFieldLabels = {
  kind: "crm.field.kind",
  name: "crm.field.name",
  isCustomer: "crm.role.customer",
  isSupplier: "crm.role.supplier",
  taxNumber: "crm.field.taxNumber",
  taxOffice: "crm.field.taxOffice",
  email: "crm.field.email",
  phone: "crm.field.phone",
  website: "crm.field.website",
  city: "crm.field.city",
  district: "crm.field.district",
  addressLine: "crm.field.address",
} as const satisfies Record<string, TranslationKey>;

const isPartyField = (field: string): field is keyof typeof partyFieldLabels => field in partyFieldLabels;

/**
 * How each entry type looks (ADR-0009). The API sends a stable type and a payload; wording and
 * icons live here so they can be translated and restyled without touching the server.
 */
const entryTypes: Record<string, EntryType> = {
  "identity.workspace.created": {
    icon: Building2,
    tone: "bg-primary/10 text-primary",
    headline: (payload, actor, t) => t("timeline.entry.workspaceCreated", { actor, workspace: text(payload.workspaceName) }),
  },
  "identity.workspace.renamed": {
    icon: PencilLine,
    tone: "bg-primary/10 text-primary",
    headline: (payload, actor, t) =>
      t("timeline.entry.workspaceRenamed", { actor, oldName: text(payload.oldName), newName: text(payload.newName) }),
  },
  "identity.member.joined": {
    icon: UserPlus,
    tone: "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400",
    headline: (payload, actor, t) => t("timeline.entry.memberJoined", { actor, role: roleLabel(text(payload.role), t) }),
  },
  "identity.invitation.sent": {
    icon: MailPlus,
    tone: "bg-sky-500/10 text-sky-600 dark:text-sky-400",
    headline: (payload, actor, t) =>
      t("timeline.entry.invitationSent", { actor, email: text(payload.email), role: roleLabel(text(payload.role), t) }),
  },
  "files.file.uploaded": {
    icon: FileUp,
    tone: "bg-violet-500/10 text-violet-600 dark:text-violet-400",
    headline: (payload, actor, t) => t("timeline.entry.fileUploaded", { actor, fileName: text(payload.fileName) }),
  },
  "crm.party.created": {
    icon: UsersRound,
    tone: "bg-primary/10 text-primary",
    headline: (payload, actor, t) =>
      t(payload.isSupplier && !payload.isCustomer ? "timeline.entry.supplierCreated" : "timeline.entry.customerCreated", {
        actor,
        party: text(payload.partyName),
      }),
    href: (payload) => partyHref(payload),
  },
  "crm.party.updated": {
    icon: PencilLine,
    tone: "bg-sky-500/10 text-sky-600 dark:text-sky-400",
    headline: (payload, actor, t) => {
      const fields = Array.isArray(payload.fields) ? payload.fields.filter((field): field is string => typeof field === "string") : [];
      const names = fields.map((field) => (isPartyField(field) ? t(partyFieldLabels[field]) : field)).join(", ");
      return t("timeline.entry.partyUpdated", { actor, party: text(payload.partyName), fields: names });
    },
    href: (payload) => partyHref(payload),
  },
  "crm.party.archived": {
    icon: Archive,
    tone: "bg-muted text-muted-foreground",
    headline: (payload, actor, t) => t("timeline.entry.partyArchived", { actor, party: text(payload.partyName) }),
  },
  "crm.contact.added": {
    icon: Contact,
    tone: "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400",
    headline: (payload, actor, t) =>
      t("timeline.entry.contactAdded", { actor, contact: text(payload.contactName), party: text(payload.partyName) }),
    href: (payload) => partyHref(payload),
  },
  "timeline.note": {
    icon: StickyNote,
    tone: "bg-amber-500/10 text-amber-600 dark:text-amber-400",
    headline: (_payload, actor, t) => t("timeline.entry.note", { actor }),
    body: (payload) => text(payload.text),
  },
};

/** A type this client does not know yet (a newer server) still renders, just generically. */
const fallback: EntryType = {
  icon: CircleDot,
  tone: "bg-muted text-muted-foreground",
  headline: (_payload, actor, t) => t("timeline.entry.unknown", { actor }),
};

export function describeEntry(item: TimelineItem, t: Translate) {
  const type = entryTypes[item.type] ?? fallback;
  const payload = (item.payload ?? {}) as Payload;
  const actor = item.actor.name ?? t("timeline.actor.system");

  return {
    icon: type.icon,
    tone: type.tone,
    headline: type.headline(payload, actor, t),
    body: type.body?.(payload),
    href: type.href?.(payload),
  };
}
