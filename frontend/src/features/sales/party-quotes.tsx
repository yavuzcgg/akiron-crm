"use client";

import { FileText, Plus } from "lucide-react";
import Link from "next/link";
import { EmptyState } from "@/components/empty-state";
import { buttonVariants } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { useI18n } from "@/lib/i18n";
import { QuoteRows } from "./quotes-list";
import { useQuotes } from "./sales-api";

/** A client's quotes on its CRM page, with a shortcut to write a new one for it. */
export function PartyQuotes({ partyId, partyName, canWrite }: { partyId: string; partyName: string; canWrite: boolean }) {
  const { t } = useI18n();
  const quotes = useQuotes({ search: "", status: "all", partyId }, 1, 10);
  const newLink = canWrite ? (
    <Link
      href={`/sales/quotes/new?partyId=${partyId}&partyName=${encodeURIComponent(partyName)}`}
      className={buttonVariants({ variant: "outline", size: "sm" })}
    >
      <Plus /> {t("sales.quote.new")}
    </Link>
  ) : null;

  if (quotes.isPending) return <Skeleton className="h-20 w-full" />;
  if ((quotes.data?.items.length ?? 0) === 0) {
    return <EmptyState icon={FileText} title={t("sales.party.empty")} className="py-6" action={newLink} />;
  }

  return (
    <div className="grid gap-3">
      <div className="-mx-6 overflow-x-auto">
        <QuoteRows items={quotes.data!.items} />
      </div>
      {newLink ? <div>{newLink}</div> : null}
    </div>
  );
}
