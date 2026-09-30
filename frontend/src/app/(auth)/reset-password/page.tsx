import type { Metadata } from "next";
import { Suspense } from "react";
import { ResetPasswordForm } from "@/features/identity/password-reset";
import { AuthCard } from "../auth-card";

export const metadata: Metadata = { title: "Yeni parola" };

export default function ResetPasswordPage() {
  return (
    <AuthCard
      title="auth.reset.title"
      description="auth.reset.description"
      switchPrompt="auth.forgot.remembered"
      switchLabel="auth.forgot.back"
      switchHref="/login"
    >
      {/* The token is read from the query string, which needs a Suspense boundary. */}
      <Suspense>
        <ResetPasswordForm />
      </Suspense>
    </AuthCard>
  );
}
