import type { Metadata } from "next";
import { Suspense } from "react";
import { LoginForm } from "@/features/identity/login-form";
import { AuthCard } from "../auth-card";

export const metadata: Metadata = { title: "Giriş" };

export default function LoginPage() {
  return (
    <AuthCard
      title="auth.login.title"
      description="auth.login.description"
      switchPrompt="auth.login.noAccount"
      switchLabel="auth.login.toRegister"
      switchHref="/register"
    >
      {/* The form reads ?next=, which needs a Suspense boundary during prerendering. */}
      <Suspense>
        <LoginForm />
      </Suspense>
    </AuthCard>
  );
}
