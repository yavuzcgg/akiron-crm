import { Building2, CircleDot, FileUp, MailPlus, StickyNote, UserPlus, type LucideIcon } from "lucide-react";
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
}

const text = (value: unknown) => (typeof value === "string" ? value : "");

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
  };
}
