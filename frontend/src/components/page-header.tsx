import type { ReactNode } from "react";

interface PageHeaderProps {
  title: string;
  description?: string;
  /** The page's one primary action, and any secondary ones before it. */
  actions?: ReactNode;
}

/** Title, one line of context and the page's primary action (design system: one per page). */
export function PageHeader({ title, description, actions }: PageHeaderProps) {
  return (
    <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
      <div className="grid gap-1">
        <h1 className="text-[22px] leading-7 font-semibold tracking-tight">{title}</h1>
        {description ? <p className="text-muted-foreground max-w-2xl">{description}</p> : null}
      </div>
      {actions ? <div className="flex shrink-0 items-center gap-2">{actions}</div> : null}
    </div>
  );
}
