import type { TranslationKey } from "@/lib/i18n";

export const stageLabels: Record<string, TranslationKey> = {
  new: "leads.stage.new",
  contacted: "leads.stage.contacted",
  meeting: "leads.stage.meeting",
  proposal: "leads.stage.proposal",
  won: "leads.stage.won",
  lost: "leads.stage.lost",
};

export const sourceLabels: Record<string, TranslationKey> = {
  website: "leads.source.website",
  web_form: "leads.source.web_form",
  referral: "leads.source.referral",
  instagram: "leads.source.instagram",
  whatsapp: "leads.source.whatsapp",
  phone: "leads.source.phone",
  email: "leads.source.email",
  event: "leads.source.event",
  other: "leads.source.other",
};

/** Column dot colours, from cold to warm; semantic tokens only. */
export const stageTone: Record<string, string> = {
  new: "bg-muted-foreground/50",
  contacted: "bg-info",
  meeting: "bg-warning",
  proposal: "bg-primary",
  won: "bg-success",
  lost: "bg-destructive",
};
