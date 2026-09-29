import type { Metadata } from "next";
import { RegisterForm } from "@/features/identity/register-form";
import { AuthCard } from "../auth-card";

export const metadata: Metadata = { title: "Kayıt" };

export default function RegisterPage() {
  return (
    <AuthCard
      title="auth.register.title"
      description="auth.register.description"
      switchPrompt="auth.register.hasAccount"
      switchLabel="auth.register.toLogin"
      switchHref="/login"
    >
      <RegisterForm />
    </AuthCard>
  );
}
