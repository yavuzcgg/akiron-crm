import type { Metadata } from "next";
import { ForgotPasswordForm } from "@/features/identity/password-reset";
import { AuthCard } from "../auth-card";

export const metadata: Metadata = { title: "Parolamı unuttum" };

export default function ForgotPasswordPage() {
  return (
    <AuthCard
      title="auth.forgot.title"
      description="auth.forgot.description"
      switchPrompt="auth.forgot.remembered"
      switchLabel="auth.forgot.back"
      switchHref="/login"
    >
      <ForgotPasswordForm />
    </AuthCard>
  );
}
