"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertCircle, Copy, KeyRound, MailPlus, Plus, UserRoundCheck, UserRoundX } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { EmptyState } from "@/components/empty-state";
import { FormField } from "@/components/form-field";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { refreshTimelineSoon } from "@/features/timeline/timeline-api";
import { api, type Schemas } from "@/lib/api/client";
import { ApiError, unwrap } from "@/lib/api/errors";
import { formatDateTime } from "@/lib/format";
import { useI18n, type TranslationKey } from "@/lib/i18n";

type PortalUser = Schemas["PortalUserResponse"];
type Invitation = Schemas["PortalInvitationResponse"];

const usersKey = (partyId: string) => ["portal-users", partyId] as const;

const statusLabels: Record<string, { label: TranslationKey; variant: "success" | "secondary" | "outline" }> = {
  active: { label: "portal.access.status.active", variant: "success" },
  invited: { label: "portal.access.status.invited", variant: "secondary" },
  disabled: { label: "portal.access.status.disabled", variant: "outline" },
};

/** The link is shown once (only its hash is kept), so it can also go out by WhatsApp. */
function LinkBox({ invitation, onDone }: { invitation: Invitation; onDone: () => void }) {
  const { t } = useI18n();
  return (
    <div className="grid gap-4">
      <Alert>
        <MailPlus />
        <AlertDescription>{t("portal.access.sent", { email: invitation.user.email })}</AlertDescription>
      </Alert>
      <div className="grid gap-1.5">
        <Label htmlFor="portal-link" className="text-[13px] font-medium">
          {t("portal.access.link")}
        </Label>
        <div className="flex gap-2">
          <Input id="portal-link" readOnly value={invitation.link} onFocus={(event) => event.target.select()} className="font-mono text-xs" />
          <Button
            variant="outline"
            size="icon"
            aria-label={t("portal.access.copy")}
            onClick={() => void navigator.clipboard.writeText(invitation.link).then(() => toast.success(t("portal.access.copied")))}
          >
            <Copy />
          </Button>
        </div>
        <p className="text-muted-foreground text-xs">{t("portal.access.linkHint")}</p>
      </div>
      <div className="flex justify-end">
        <Button onClick={onDone}>{t("common.close")}</Button>
      </div>
    </div>
  );
}

interface PortalAccessProps {
  partyId: string;
  /** The party's contacts, offered as one-click candidates. */
  contacts: { fullName: string; email?: string | null }[];
  canManage: boolean;
}

/** Who of a client's people can open the portal: invite a contact, send a fresh link, switch access off. */
export function PortalAccess({ partyId, contacts, canManage }: PortalAccessProps) {
  const { t, tError } = useI18n();
  const queryClient = useQueryClient();
  const [inviting, setInviting] = useState(false);
  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [invitation, setInvitation] = useState<Invitation | null>(null);

  const users = useQuery({
    queryKey: usersKey(partyId),
    queryFn: async () => unwrap(await api.GET("/api/v1/portal/users", { params: { query: { partyId } } })),
  });
  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: usersKey(partyId) });
    refreshTimelineSoon(queryClient);
  };
  const fail = (failure: unknown) => toast.error(tError(failure instanceof ApiError ? failure.code : "common.network"));

  const invite = useMutation({
    mutationFn: async (body: { fullName: string; email: string }) => unwrap(await api.POST("/api/v1/portal/users", { body: { partyId, ...body } })),
    onSuccess: refresh,
  });
  const resend = useMutation({
    mutationFn: async (id: string) => unwrap(await api.POST("/api/v1/portal/users/{id}/invite", { params: { path: { id } } })),
    onSuccess: (result) => {
      refresh();
      setInvitation(result);
    },
    onError: fail,
  });
  const setAccess = useMutation({
    mutationFn: async ({ id, enabled }: { id: string; enabled: boolean }) =>
      unwrap(await api.POST(enabled ? "/api/v1/portal/users/{id}/enable" : "/api/v1/portal/users/{id}/disable", { params: { path: { id } } })),
    onSuccess: refresh,
    onError: fail,
  });

  const known = new Set((users.data ?? []).map((user) => user.email));
  const candidates = contacts.filter((contact) => contact.email && !known.has(contact.email.toLowerCase()));

  const open = (contact?: { fullName: string; email?: string | null }) => {
    setFullName(contact?.fullName ?? "");
    setEmail(contact?.email ?? "");
    setError(null);
    setInvitation(null);
    setInviting(true);
  };

  const submit = async () => {
    if (!fullName.trim() || !email.trim()) {
      setError(t("portal.access.required"));
      return;
    }
    setError(null);
    try {
      setInvitation(await invite.mutateAsync({ fullName: fullName.trim(), email: email.trim() }));
    } catch (failure) {
      setError(tError(failure instanceof ApiError ? failure.code : "common.network"));
    }
  };

  const add = canManage ? (
    <Button variant="outline" size="sm" onClick={() => open(candidates[0])}>
      <Plus /> {t("portal.access.invite")}
    </Button>
  ) : null;

  return (
    <div className="grid gap-3">
      {users.isPending ? (
        <Skeleton className="h-16 w-full" />
      ) : (users.data ?? []).length === 0 ? (
        <EmptyState icon={KeyRound} title={t("portal.access.empty")} className="py-6" action={add} />
      ) : (
        <>
          <ul className="divide-border divide-y">
            {users.data!.map((user: PortalUser) => {
              const status = statusLabels[user.status] ?? statusLabels.invited!;
              return (
                <li key={user.id} className="flex items-center gap-3 py-2.5 first:pt-0 last:pb-0">
                  <div className="grid min-w-0 flex-1 gap-0.5">
                    <span className="flex flex-wrap items-center gap-2 text-sm font-medium">
                      <span className="truncate">{user.fullName}</span>
                      <Badge variant={status.variant}>{t(status.label)}</Badge>
                    </span>
                    <span className="text-muted-foreground truncate text-xs">
                      {user.email}
                      {user.lastSignInAt ? ` · ${t("portal.access.lastSeen", { date: formatDateTime(user.lastSignInAt) })}` : ""}
                    </span>
                  </div>
                  {canManage ? (
                    <div className="flex shrink-0 gap-1">
                      {user.status !== "disabled" ? (
                        <Button variant="ghost" size="icon-sm" title={t("portal.access.resend")} aria-label={t("portal.access.resend")} disabled={resend.isPending} onClick={() => resend.mutate(user.id)}>
                          <MailPlus />
                        </Button>
                      ) : null}
                      <Button
                        variant="ghost"
                        size="icon-sm"
                        title={t(user.status === "disabled" ? "portal.access.enable" : "portal.access.disable")}
                        aria-label={t(user.status === "disabled" ? "portal.access.enable" : "portal.access.disable")}
                        onClick={() => setAccess.mutate({ id: user.id, enabled: user.status === "disabled" })}
                      >
                        {user.status === "disabled" ? <UserRoundCheck /> : <UserRoundX />}
                      </Button>
                    </div>
                  ) : null}
                </li>
              );
            })}
          </ul>
          {add ? <div>{add}</div> : null}
        </>
      )}

      <Dialog
        open={inviting || invitation !== null}
        onOpenChange={(next) => {
          if (!next) {
            setInviting(false);
            setInvitation(null);
          }
        }}
      >
        <DialogContent className="sm:max-w-md" closeLabel={t("common.cancel")}>
          <DialogHeader>
            <DialogTitle>{t("portal.access.inviteTitle")}</DialogTitle>
            <DialogDescription>{t("portal.access.inviteHint")}</DialogDescription>
          </DialogHeader>
          {invitation ? (
            <LinkBox
              invitation={invitation}
              onDone={() => {
                setInviting(false);
                setInvitation(null);
              }}
            />
          ) : (
            <div className="grid gap-4">
              {error ? (
                <Alert variant="destructive">
                  <AlertCircle />
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              ) : null}
              {candidates.length > 1 ? (
                <div className="flex flex-wrap gap-1.5">
                  {candidates.map((contact) => (
                    <Button key={contact.email} variant="outline" size="sm" onClick={() => open(contact)}>
                      {contact.fullName}
                    </Button>
                  ))}
                </div>
              ) : null}
              <FormField id="portal-invite-name" label={t("portal.access.name")} value={fullName} onChange={(event) => setFullName(event.target.value)} maxLength={200} />
              <FormField id="portal-invite-email" type="email" label={t("crm.field.email")} value={email} onChange={(event) => setEmail(event.target.value)} maxLength={254} />
              <div className="flex justify-end gap-2">
                <Button variant="outline" onClick={() => setInviting(false)}>
                  {t("common.cancel")}
                </Button>
                <Button onClick={() => void submit()} disabled={invite.isPending}>
                  {invite.isPending ? t("common.saving") : t("portal.access.send")}
                </Button>
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}

/** What every client reads in the portal: a greeting, and how to pay (IBAN, account name). */
export function PortalSettingsForm({ canManage }: { canManage: boolean }) {
  const { t, tError } = useI18n();
  const queryClient = useQueryClient();
  const settings = useQuery({ queryKey: ["portal-settings"], queryFn: async () => unwrap(await api.GET("/api/v1/portal/settings")) });
  const save = useMutation({
    mutationFn: async (body: Schemas["PortalSettingsCommand"]) => unwrap(await api.PUT("/api/v1/portal/settings", { body })),
    onSuccess: (saved) => {
      queryClient.setQueryData(["portal-settings"], saved);
      toast.success(t("portal.settings.saved"));
    },
    onError: (failure) => toast.error(tError(failure instanceof ApiError ? failure.code : "common.network")),
  });

  if (settings.isPending) return <Skeleton className="h-60 w-full" />;
  if (settings.isError) {
    return (
      <Alert variant="destructive">
        <AlertCircle />
        <AlertDescription>{tError(settings.error instanceof ApiError ? settings.error.code : "common.network")}</AlertDescription>
      </Alert>
    );
  }

  return <SettingsFields key={`${settings.data.welcomeNote}|${settings.data.paymentInstructions}`} initial={settings.data} canManage={canManage} pending={save.isPending} onSave={(body) => save.mutate(body)} />;
}

function SettingsFields({
  initial,
  canManage,
  pending,
  onSave,
}: {
  initial: Schemas["PortalSettingsResponse"];
  canManage: boolean;
  pending: boolean;
  onSave: (body: Schemas["PortalSettingsCommand"]) => void;
}) {
  const { t } = useI18n();
  const [welcomeNote, setWelcomeNote] = useState(initial.welcomeNote ?? "");
  const [paymentInstructions, setPaymentInstructions] = useState(initial.paymentInstructions ?? "");

  return (
    <div className="grid gap-5">
      <div className="grid gap-1.5">
        <Label htmlFor="portal-welcome" className="text-[13px] font-medium">
          {t("portal.settings.welcome")}
        </Label>
        <Textarea id="portal-welcome" rows={3} maxLength={2000} disabled={!canManage} value={welcomeNote} onChange={(event) => setWelcomeNote(event.target.value)} placeholder={t("portal.settings.welcomePlaceholder")} />
        <p className="text-muted-foreground text-xs">{t("portal.settings.welcomeHint")}</p>
      </div>
      <div className="grid gap-1.5">
        <Label htmlFor="portal-payment" className="text-[13px] font-medium">
          {t("portal.settings.payment")}
        </Label>
        <Textarea
          id="portal-payment"
          rows={4}
          maxLength={2000}
          disabled={!canManage}
          className="font-mono"
          value={paymentInstructions}
          onChange={(event) => setPaymentInstructions(event.target.value)}
          placeholder={t("portal.settings.paymentPlaceholder")}
        />
        <p className="text-muted-foreground text-xs">{t("portal.settings.paymentHint")}</p>
      </div>
      {canManage ? (
        <div>
          <Button disabled={pending} onClick={() => onSave({ welcomeNote: welcomeNote.trim() || null, paymentInstructions: paymentInstructions.trim() || null })}>
            {pending ? t("common.saving") : t("common.save")}
          </Button>
        </div>
      ) : null}
    </div>
  );
}
