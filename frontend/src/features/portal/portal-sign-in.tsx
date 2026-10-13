"use client";

import { AlertCircle, MailCheck } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState, type FormEvent, type ReactNode } from "react";
import { FormField } from "@/components/form-field";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { ApiError } from "@/lib/api/errors";
import { useI18n } from "@/lib/i18n";
import { useEnterPortal, useRequestLink } from "./portal-api";

function Frame({ children }: { children: ReactNode }) {
  const { t } = useI18n();
  return (
    <main className="bg-background flex min-h-dvh flex-col items-center justify-center gap-6 px-4 py-10">
      <div className="bg-card grid w-full max-w-sm gap-5 rounded-xl border p-6 shadow-xs">{children}</div>
      <p className="text-muted-foreground text-xs">{t("portal.poweredBy")}</p>
    </main>
  );
}

/** One field, one button: the client asks for a sign-in link. The answer is the same for any address. */
export function PortalLogin() {
  const { t, tError } = useI18n();
  const request = useRequestLink();
  const [email, setEmail] = useState("");
  const [sentTo, setSentTo] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    try {
      await request.mutateAsync(email.trim());
      setSentTo(email.trim());
    } catch (failure) {
      setError(tError(failure instanceof ApiError ? failure.code : "common.network"));
    }
  };

  if (sentTo) {
    return (
      <Frame>
        <span className="bg-success-soft text-success flex size-11 items-center justify-center rounded-full">
          <MailCheck className="size-5" aria-hidden />
        </span>
        <div className="grid gap-1.5">
          <h1 className="text-xl font-semibold tracking-tight">{t("portal.login.sentTitle")}</h1>
          <p className="text-muted-foreground text-sm">{t("portal.login.sentBody", { email: sentTo })}</p>
        </div>
        <Button variant="outline" onClick={() => setSentTo(null)}>
          {t("portal.login.another")}
        </Button>
      </Frame>
    );
  }

  return (
    <Frame>
      <div className="grid gap-1.5">
        <h1 className="text-xl font-semibold tracking-tight">{t("portal.login.title")}</h1>
        <p className="text-muted-foreground text-sm">{t("portal.login.description")}</p>
      </div>
      <form onSubmit={submit} className="grid gap-4">
        {error ? (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        ) : null}
        <FormField id="portal-email" type="email" autoComplete="email" required label={t("portal.login.email")} value={email} onChange={(event) => setEmail(event.target.value)} className="text-base md:text-sm" />
        <Button type="submit" size="lg" disabled={request.isPending || !email.trim()}>
          {request.isPending ? t("portal.login.sending") : t("portal.login.submit")}
        </Button>
      </form>
    </Frame>
  );
}

/** The page behind the e-mailed link: trades the token for a session, then opens the portal. */
export function PortalEnter({ token }: { token: string | null }) {
  const { t, tError } = useI18n();
  const router = useRouter();
  const enter = useEnterPortal();
  const started = useRef(false);

  useEffect(() => {
    // A link is single-use; never post it twice (React runs effects twice in development).
    if (!token || started.current) return;
    started.current = true;
    enter.mutate(token, { onSuccess: () => router.replace("/portal") });
  }, [token, enter, router]);

  if (!token || enter.isError) {
    return (
      <Frame>
        <Alert variant="destructive">
          <AlertCircle />
          <AlertDescription>{token ? tError(enter.error instanceof ApiError ? enter.error.code : "common.network") : tError("portal.link.invalid")}</AlertDescription>
        </Alert>
        <Link href="/portal/login" className={buttonVariants()}>
          {t("portal.enter.newLink")}
        </Link>
      </Frame>
    );
  }

  return (
    <Frame>
      <p className="text-sm font-medium" role="status">
        {t("portal.enter.working")}
      </p>
      <Skeleton className="h-2 w-full" />
    </Frame>
  );
}
