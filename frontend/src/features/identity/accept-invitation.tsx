"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertCircle } from "lucide-react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useState, type FormEvent } from "react";
import { FormField } from "@/components/form-field";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { api } from "@/lib/api/client";
import { ApiError, unwrap } from "@/lib/api/errors";
import { useI18n } from "@/lib/i18n";
import { roleLabel } from "./role-name";
import { sessionQueryKey } from "./session";

const passwordMinLength = 10;

export function AcceptInvitation() {
  const { t, tError } = useI18n();
  const router = useRouter();
  const queryClient = useQueryClient();
  const token = useSearchParams().get("token") ?? "";

  const [fullName, setFullName] = useState("");
  const [password, setPassword] = useState("");
  const [formError, setFormError] = useState<string | null>(null);

  const preview = useQuery({
    queryKey: ["identity", "invitation-preview", token],
    queryFn: async () =>
      unwrap(await api.GET("/api/v1/identity/invitations/preview", { params: { query: { token } } })),
    enabled: token.length > 0,
    retry: false,
  });

  const accept = useMutation({
    mutationFn: async () =>
      unwrap(
        await api.POST("/api/v1/identity/invitations/accept", {
          body: { token, password, fullName: preview.data?.accountExists ? null : fullName },
        }),
      ),
    onSuccess: (session) => {
      queryClient.setQueryData(sessionQueryKey, session);
      router.replace("/dashboard");
    },
  });

  if (!token || preview.isError) {
    return (
      <div className="grid gap-6">
        <div className="grid gap-2">
          <h1 className="text-[28px] leading-tight font-bold tracking-[-0.01em]">{t("invite.title")}</h1>
          <p className="text-muted-foreground text-[15px]">{t("invite.invalid")}</p>
        </div>
        <Link href="/login" className={buttonVariants({ variant: "outline", size: "lg" })}>
          {t("invite.toLogin")}
        </Link>
      </div>
    );
  }

  if (preview.isPending) return <Skeleton className="h-64 w-full" />;

  const invitation = preview.data;
  const isNewAccount = !invitation.accountExists;

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setFormError(null);

    if (isNewAccount && !fullName.trim()) return setFormError(t("error.identity.invitation.full_name_required"));
    if (isNewAccount && password.length < passwordMinLength)
      return setFormError(t("validation.min_length", { minLength: passwordMinLength }));

    try {
      await accept.mutateAsync();
    } catch (error) {
      setFormError(error instanceof ApiError ? tError(error.code, error.params) : tError("common.network"));
    }
  };

  return (
    <div className="grid gap-8">
      <div className="grid gap-2">
        <h1 className="text-[28px] leading-tight font-bold tracking-[-0.01em]">{invitation.workspaceName}</h1>
        <p className="text-muted-foreground text-[15px]">
          {t("invite.description", {
            inviter: invitation.invitedByName,
            workspace: invitation.workspaceName,
            role: roleLabel(invitation.role, t),
          })}
        </p>
      </div>
        <form onSubmit={submit} className="grid gap-4" noValidate>
          <p className="text-muted-foreground text-sm">
            {t(isNewAccount ? "invite.newAccount" : "invite.existingAccount", { email: invitation.email })}
          </p>
          {formError ? (
            <Alert variant="destructive">
              <AlertCircle />
              <AlertDescription>{formError}</AlertDescription>
            </Alert>
          ) : null}
          {isNewAccount ? (
            <FormField
              id="fullName"
              autoComplete="name"
              label={t("auth.field.fullName")}
              value={fullName}
              onChange={(event) => setFullName(event.target.value)}
            />
          ) : null}
          <FormField
            id="password"
            type="password"
            autoComplete={isNewAccount ? "new-password" : "current-password"}
            label={t("auth.field.password")}
            hint={isNewAccount ? t("auth.field.passwordHint", { minLength: passwordMinLength }) : undefined}
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
          <Button type="submit" size="lg" className="mt-1 w-full" disabled={accept.isPending}>
            {accept.isPending ? t("common.loading") : t("invite.submit")}
          </Button>
        </form>
    </div>
  );
}
