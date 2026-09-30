"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AlertCircle, Lock } from "lucide-react";
import { useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { FormField } from "@/components/form-field";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { refreshTimelineSoon } from "@/features/timeline/timeline-api";
import { api } from "@/lib/api/client";
import { unwrap } from "@/lib/api/errors";
import { useI18n } from "@/lib/i18n";
import { applyApiError } from "./form-errors";
import { sessionQueryKey, type Session } from "./session";

/** API limits (Identity validators). */
export const accountLimits = { name: 200, passwordMin: 10, passwordMax: 128 } as const;

function FormAlert({ message }: { message: string | null }) {
  if (!message) return null;
  return (
    <Alert variant="destructive">
      <AlertCircle />
      <AlertDescription>{message}</AlertDescription>
    </Alert>
  );
}

const profileFields = ["fullName"] as const;

export function ProfileForm({ session }: { session: Session }) {
  const { t, tError } = useI18n();
  const queryClient = useQueryClient();
  const [formError, setFormError] = useState<string | null>(null);

  const save = useMutation({
    mutationFn: async (body: { fullName: string }) =>
      unwrap(await api.PUT("/api/v1/identity/me/profile", { body })),
    onSuccess: () => {
      toast.success(t("account.profile.saved"));
      void queryClient.invalidateQueries({ queryKey: sessionQueryKey });
    },
  });

  const schema = useMemo(
    () =>
      z.object({
        fullName: z
          .string()
          .trim()
          .min(1, t("validation.required"))
          .max(accountLimits.name, t("validation.max_length", { maxLength: accountLimits.name })),
      }),
    [t],
  );

  const form = useForm<z.infer<typeof schema>>({
    resolver: zodResolver(schema),
    values: { fullName: session.fullName },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await save.mutateAsync(values);
    } catch (error) {
      setFormError(applyApiError(error, profileFields, form.setError, tError));
    }
  });

  return (
    <form onSubmit={onSubmit} className="grid max-w-md gap-4" noValidate>
      <FormAlert message={formError} />
      <FormField
        id="fullName"
        autoComplete="name"
        label={t("auth.field.fullName")}
        error={form.formState.errors.fullName?.message}
        {...form.register("fullName")}
      />
      <FormField
        id="email"
        type="email"
        label={t("auth.field.email")}
        value={session.email}
        readOnly
        disabled
        hint={t("account.profile.email")}
      />
      <div>
        <Button type="submit" disabled={save.isPending || !form.formState.isDirty}>
          {save.isPending ? t("common.saving") : t("common.save")}
        </Button>
      </div>
    </form>
  );
}

const passwordFields = ["currentPassword", "newPassword"] as const;

export function ChangePasswordForm() {
  const { t, tError } = useI18n();
  const [formError, setFormError] = useState<string | null>(null);

  const change = useMutation({
    mutationFn: async (body: { currentPassword: string; newPassword: string }) =>
      unwrap(await api.PUT("/api/v1/identity/auth/password", { body })),
    onSuccess: () => toast.success(t("account.password.saved")),
  });

  const schema = useMemo(
    () =>
      z.object({
        currentPassword: z.string().min(1, t("validation.required")),
        newPassword: z
          .string()
          .min(accountLimits.passwordMin, t("validation.min_length", { minLength: accountLimits.passwordMin }))
          .max(accountLimits.passwordMax, t("validation.max_length", { maxLength: accountLimits.passwordMax })),
      }),
    [t],
  );

  const form = useForm<z.infer<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: { currentPassword: "", newPassword: "" },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await change.mutateAsync(values);
      form.reset();
    } catch (error) {
      setFormError(applyApiError(error, passwordFields, form.setError, tError));
    }
  });

  const { errors } = form.formState;

  return (
    <form onSubmit={onSubmit} className="grid max-w-md gap-4" noValidate>
      <FormAlert message={formError} />
      <FormField
        id="currentPassword"
        type="password"
        autoComplete="current-password"
        label={t("auth.field.currentPassword")}
        error={errors.currentPassword?.message}
        {...form.register("currentPassword")}
      />
      <FormField
        id="newPassword"
        type="password"
        autoComplete="new-password"
        label={t("auth.field.newPassword")}
        hint={t("auth.field.passwordHint", { minLength: accountLimits.passwordMin })}
        error={errors.newPassword?.message}
        {...form.register("newPassword")}
      />
      <div>
        <Button type="submit" disabled={change.isPending}>
          <Lock /> {change.isPending ? t("common.saving") : t("account.password.submit")}
        </Button>
      </div>
    </form>
  );
}

const workspaceFields = ["name"] as const;

export function WorkspaceNameForm({ session, canManage }: { session: Session; canManage: boolean }) {
  const { t, tError } = useI18n();
  const queryClient = useQueryClient();
  const [formError, setFormError] = useState<string | null>(null);

  const rename = useMutation({
    mutationFn: async (body: { name: string }) => unwrap(await api.PUT("/api/v1/identity/workspace", { body })),
    onSuccess: () => {
      toast.success(t("workspace.name.saved"));
      void queryClient.invalidateQueries({ queryKey: sessionQueryKey });
      refreshTimelineSoon(queryClient);
    },
  });

  const schema = useMemo(
    () =>
      z.object({
        name: z
          .string()
          .trim()
          .min(1, t("validation.required"))
          .max(accountLimits.name, t("validation.max_length", { maxLength: accountLimits.name })),
      }),
    [t],
  );

  const form = useForm<z.infer<typeof schema>>({
    resolver: zodResolver(schema),
    values: { name: session.tenantName },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await rename.mutateAsync(values);
    } catch (error) {
      setFormError(applyApiError(error, workspaceFields, form.setError, tError));
    }
  });

  return (
    <form onSubmit={onSubmit} className="grid max-w-md gap-4" noValidate>
      <FormAlert message={formError} />
      <FormField
        id="name"
        autoComplete="organization"
        label={t("workspace.name.label")}
        disabled={!canManage}
        hint={canManage ? undefined : t("workspace.ownerOnly")}
        error={form.formState.errors.name?.message}
        {...form.register("name")}
      />
      {canManage ? (
        <div>
          <Button type="submit" disabled={rename.isPending || !form.formState.isDirty}>
            {rename.isPending ? t("common.saving") : t("common.save")}
          </Button>
        </div>
      ) : null}
    </form>
  );
}
