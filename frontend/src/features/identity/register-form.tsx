"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { AlertCircle } from "lucide-react";
import { useRouter } from "next/navigation";
import { useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { FormField } from "@/components/form-field";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { useI18n } from "@/lib/i18n";
import { applyApiError } from "./form-errors";
import { useRegister } from "./session";

/** Mirrors RegisterValidator on the API; the API stays the authority. */
const limits = { name: 200, emailMax: 254, passwordMin: 10, passwordMax: 128 } as const;

const fields = ["organizationName", "fullName", "email", "password"] as const;

export function RegisterForm() {
  const { t, tError } = useI18n();
  const router = useRouter();
  const register = useRegister();
  const [formError, setFormError] = useState<string | null>(null);

  const schema = useMemo(() => {
    const required = t("validation.required");
    const maxName = t("validation.max_length", { maxLength: limits.name });

    return z.object({
      organizationName: z.string().trim().min(1, required).max(limits.name, maxName),
      fullName: z.string().trim().min(1, required).max(limits.name, maxName),
      email: z
        .string()
        .trim()
        .min(1, required)
        .email(t("validation.email"))
        .max(limits.emailMax, t("validation.max_length", { maxLength: limits.emailMax })),
      password: z
        .string()
        .min(limits.passwordMin, t("validation.min_length", { minLength: limits.passwordMin }))
        .max(limits.passwordMax, t("validation.max_length", { maxLength: limits.passwordMax })),
    });
  }, [t]);

  const form = useForm<z.infer<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: { organizationName: "", fullName: "", email: "", password: "" },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await register.mutateAsync(values);
      router.replace("/dashboard");
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
        id="organizationName"
        autoComplete="organization"
        label={t("auth.field.organizationName")}
        error={errors.organizationName?.message}
        {...form.register("organizationName")}
      />
      <FormField
        id="fullName"
        autoComplete="name"
        label={t("auth.field.fullName")}
        error={errors.fullName?.message}
        {...form.register("fullName")}
      />
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
        autoComplete="new-password"
        label={t("auth.field.password")}
        hint={t("auth.field.passwordHint", { minLength: limits.passwordMin })}
        error={errors.password?.message}
        {...form.register("password")}
      />
      <Button type="submit" size="lg" disabled={register.isPending}>
        {register.isPending ? t("common.loading") : t("auth.register.submit")}
      </Button>
    </form>
  );
}
