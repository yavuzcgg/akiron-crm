"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import { AlertCircle, CircleCheck, MailCheck } from "lucide-react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { FormField } from "@/components/form-field";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import { api } from "@/lib/api/client";
import { unwrap } from "@/lib/api/errors";
import { useI18n } from "@/lib/i18n";
import { accountLimits } from "./account-settings";
import { applyApiError } from "./form-errors";

export function ForgotPasswordForm() {
  const { t, tError } = useI18n();
  const [sentTo, setSentTo] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  const send = useMutation({
    mutationFn: async (body: { email: string }) =>
      unwrap(await api.POST("/api/v1/identity/auth/forgot-password", { body })),
  });

  const schema = useMemo(
    () => z.object({ email: z.string().trim().min(1, t("validation.required")).email(t("validation.email")) }),
    [t],
  );

  const form = useForm<z.infer<typeof schema>>({ resolver: zodResolver(schema), defaultValues: { email: "" } });

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await send.mutateAsync(values);
      setSentTo(values.email);
    } catch (error) {
      setFormError(applyApiError(error, ["email"] as const, form.setError, tError));
    }
  });

  if (sentTo) {
    return (
      <Alert>
        <MailCheck />
        <AlertDescription>{t("auth.forgot.sent", { email: sentTo })}</AlertDescription>
      </Alert>
    );
  }

  return (
    <form onSubmit={onSubmit} className="grid gap-4" noValidate>
      {formError ? (
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>{formError}</AlertDescription>
        </Alert>
      ) : null}
      <FormField
        id="email"
        type="email"
        autoComplete="email"
        label={t("auth.field.email")}
        error={form.formState.errors.email?.message}
        {...form.register("email")}
      />
      <Button type="submit" size="lg" className="mt-1 w-full" disabled={send.isPending}>
        {send.isPending ? t("common.loading") : t("auth.forgot.submit")}
      </Button>
    </form>
  );
}

export function ResetPasswordForm() {
  const { t, tError } = useI18n();
  const token = useSearchParams().get("token") ?? "";
  const [done, setDone] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const reset = useMutation({
    mutationFn: async (body: { token: string; newPassword: string }) =>
      unwrap(await api.POST("/api/v1/identity/auth/reset-password", { body })),
  });

  const schema = useMemo(
    () =>
      z.object({
        newPassword: z
          .string()
          .min(accountLimits.passwordMin, t("validation.min_length", { minLength: accountLimits.passwordMin }))
          .max(accountLimits.passwordMax, t("validation.max_length", { maxLength: accountLimits.passwordMax })),
      }),
    [t],
  );

  const form = useForm<z.infer<typeof schema>>({ resolver: zodResolver(schema), defaultValues: { newPassword: "" } });

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await reset.mutateAsync({ token, newPassword: values.newPassword });
      setDone(true);
    } catch (error) {
      setFormError(applyApiError(error, ["newPassword"] as const, form.setError, tError));
    }
  });

  if (!token) {
    return (
      <div className="grid gap-4">
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>{t("auth.reset.missingToken")}</AlertDescription>
        </Alert>
        <Link href="/forgot-password" className={buttonVariants({ variant: "outline", size: "lg" })}>
          {t("auth.reset.requestNew")}
        </Link>
      </div>
    );
  }

  if (done) {
    return (
      <div className="grid gap-4">
        <Alert>
          <CircleCheck />
          <AlertDescription>{t("auth.reset.done")}</AlertDescription>
        </Alert>
        <Link href="/login" className={buttonVariants({ size: "lg" })}>
          {t("auth.login.submit")}
        </Link>
      </div>
    );
  }

  return (
    <form onSubmit={onSubmit} className="grid gap-4" noValidate>
      {formError ? (
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>
            {formError}{" "}
            <Link href="/forgot-password" className="font-semibold underline underline-offset-4">
              {t("auth.reset.requestNew")}
            </Link>
          </AlertDescription>
        </Alert>
      ) : null}
      <FormField
        id="newPassword"
        type="password"
        autoComplete="new-password"
        label={t("auth.field.newPassword")}
        hint={t("auth.field.passwordHint", { minLength: accountLimits.passwordMin })}
        error={form.formState.errors.newPassword?.message}
        {...form.register("newPassword")}
      />
      <Button type="submit" size="lg" className="mt-1 w-full" disabled={reset.isPending}>
        {reset.isPending ? t("common.loading") : t("auth.reset.submit")}
      </Button>
    </form>
  );
}
