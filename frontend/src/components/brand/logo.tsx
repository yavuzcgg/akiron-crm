import { useId } from "react";
import { cn } from "@/lib/utils";

/**
 * The Akiron mark: an "A" folded from one ribbon (front leg white, back leg indigo-200) with an
 * emerald dot between the legs, the collected payment at the end of the work. Same drawing as
 * `src/app/icon.svg` (favicon); keep the two in step.
 */
export function LogoMark({
  className,
  title = "Akiron",
  decorative = false,
}: {
  className?: string;
  title?: string;
  /** Next to visible "Akiron" text: hide the mark from screen readers instead of naming it twice. */
  decorative?: boolean;
}) {
  // Unique per instance: two marks on one page (one hidden) must not share a gradient id, or the
  // visible one paints with the hidden one's gradient and disappears.
  const id = useId().replace(/[^a-zA-Z0-9_-]/g, "");
  const bg = `akiron-bg-${id}`;
  const sheen = `akiron-sheen-${id}`;

  return (
    <svg
      viewBox="0 0 64 64"
      {...(decorative ? { "aria-hidden": true } : { role: "img", "aria-label": title })}
      className={cn("size-8 shrink-0", className)}
    >
      <defs>
        <linearGradient id={bg} x1="0" y1="64" x2="64" y2="0" gradientUnits="userSpaceOnUse">
          <stop offset="0" stopColor="#3730A3" />
          <stop offset="1" stopColor="#6366F1" />
        </linearGradient>
        <linearGradient id={sheen} x1="0" y1="0" x2="0" y2="64" gradientUnits="userSpaceOnUse">
          <stop offset="0" stopColor="#fff" stopOpacity="0.18" />
          <stop offset="0.5" stopColor="#fff" stopOpacity="0" />
        </linearGradient>
      </defs>
      <rect width="64" height="64" rx="15" fill={`url(#${bg})`} />
      <rect width="64" height="64" rx="15" fill={`url(#${sheen})`} />
      <path d="M30 13h8l12.5 37h-8.6z" fill="#C7D2FE" stroke="#C7D2FE" strokeWidth="2.4" strokeLinejoin="round" />
      <path d="M26 13h8L21.6 50H13z" fill="#fff" stroke="#fff" strokeWidth="2.4" strokeLinejoin="round" />
      <circle cx="32" cy="44" r="4.2" fill="#34D399" />
    </svg>
  );
}

/** Mark plus word; the word hides when the sidebar collapses to icons. */
export function Logo({ className, subtitle }: { className?: string; subtitle?: string }) {
  return (
    <span className={cn("flex items-center gap-2.5", className)}>
      <LogoMark />
      <span className="grid leading-none group-data-[collapsible=icon]:hidden">
        <span className="text-[15px] font-bold tracking-tight">akiron</span>
        {subtitle ? <span className="text-muted-foreground mt-1 text-[11px] font-medium">{subtitle}</span> : null}
      </span>
    </span>
  );
}
