import { useId } from "react";
import { cn } from "@/lib/utils";

/**
 * The Akiron mark: two strokes rising to a point (an "A", and a curve going up) with a dot where a
 * timeline begins. Drawn in SVG so it stays sharp at any size and follows the theme.
 */
export function LogoMark({ className, title = "Akiron" }: { className?: string; title?: string }) {
  // Unique per instance: two marks on one page (one hidden) must not share a gradient id, or the
  // visible one paints with the hidden one's gradient and disappears.
  const gradientId = `akiron-mark-${useId().replace(/[^a-zA-Z0-9_-]/g, "")}`;

  return (
    <svg viewBox="0 0 32 32" role="img" aria-label={title} className={cn("size-8 shrink-0", className)}>
      <defs>
        <linearGradient id={gradientId} x1="0" y1="32" x2="32" y2="0" gradientUnits="userSpaceOnUse">
          <stop offset="0" stopColor="#4338CA" />
          <stop offset="1" stopColor="#6366F1" />
        </linearGradient>
      </defs>
      <rect width="32" height="32" rx="9" fill={`url(#${gradientId})`} />
      <path d="M9 23 L16 8.5 L23 23" fill="none" stroke="#fff" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M12.4 17.5 H19.6" fill="none" stroke="#fff" strokeOpacity="0.55" strokeWidth="2.4" strokeLinecap="round" />
      <circle cx="23" cy="23" r="2.2" fill="#34D399" />
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
