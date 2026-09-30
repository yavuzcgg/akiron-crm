"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { AlertCircle } from "lucide-react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { FormField } from "@/components/form-field";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { useI18n } from "@/lib/i18n";
import { applyApiError } from "./form-errors";
import { useLogin } from "./session";

const fields = ["email", "password"] as const;

/** Only same-site paths, so a crafted ?next= cannot send the user to another site after sign-in. */
function safeNext(next: string | null): string {
  return next && next.startsWith("/") && !next.startsWith("//") ? next : "/dashboard";
}

export function LoginForm() {
  const { t, tError } = useI18n();
  const router = useRouter();
  const searchParams = useSearchParams();
  const login = useLogin();
  const [formError, setFormError] = useState<string | null>(null);

  const schema = useMemo(
    () =>
      z.object({
        email: z.string().trim().min(1, t("validation.required")).email(t("validation.email")),
        password: z.string().min(1, t("validation.required")),
      }),
    [t],
  );

  const form = useForm<z.infer<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: { email: "", password: "" },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await login.mutateAsync(values);
      router.replace(safeNext(searchParams.get("next")));
    } catch (error) {
      setFormError(applyApiError(error, fields, form.setError, tError));
    }
  });

  const { errors } = form.formState;

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
        error={errors.email?.message}
        {...form.register("email")}
      />
      <FormField
        id="password"
        type="password"
        autoComplete="current-password"
        label={t("auth.field.password")}
        error={errors.password?.message}
        {...form.register("password")}
      />
      <Link href="/forgot-password" className="text-primary -mt-2 justify-self-end text-[13px] font-medium underline-offset-4 hover:underline">
        {t("auth.login.forgot")}
      </Link>
      <Button type="submit" size="lg" className="mt-1 w-full" disabled={login.isPending}>
        {login.isPending ? t("common.loading") : t("auth.login.submit")}
      </Button>
    </form>
  );
}
