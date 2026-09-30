"use client";

import { Building2, X } from "lucide-react";
import { useEffect, useId, useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useClientSearch } from "./jobs-api";

export interface PickedClient {
  partyId: string;
  name: string;
}

interface ClientPickerProps {
  id: string;
  value: PickedClient | null;
  onChange: (client: PickedClient | null) => void;
}

/**
 * A combobox over the tenant's customers (names only, through Jobs, so people without CRM access
 * can still link a client). Arrow keys move, Enter picks, Escape closes.
 */
export function ClientPicker({ id, value, onChange }: ClientPickerProps) {
  const { t } = useI18n();
  const listId = useId();
  const [query, setQuery] = useState("");
  const [debounced, setDebounced] = useState("");
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);

  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(query.trim()), 200);
    return () => window.clearTimeout(timer);
  }, [query]);

  const clients = useClientSearch(debounced, open);
  const options = clients.data ?? [];

  const pick = (index: number) => {
    const option = options[index];
    if (!option) return;
    onChange({ partyId: option.partyId, name: option.name });
    setQuery("");
    setOpen(false);
  };

  if (value) {
    return (
      <div className="grid gap-1.5">
        <Label htmlFor={id} className="text-[13px] font-medium">
          {t("jobs.field.client")}
        </Label>
        <div className="border-input flex h-10 items-center gap-2 rounded-lg border px-3 text-sm">
          <Building2 className="text-muted-foreground size-4" aria-hidden />
          <span id={id} className="flex-1 truncate">
            {value.name}
          </span>
          <Button type="button" variant="ghost" size="icon-xs" onClick={() => onChange(null)} aria-label={t("jobs.client.clear")}>
            <X />
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="relative grid gap-1.5">
      <Label htmlFor={id} className="text-[13px] font-medium">
        {t("jobs.field.client")}
      </Label>
      <Input
        id={id}
        role="combobox"
        aria-expanded={open}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-activedescendant={open && options[active] ? `${listId}-${active}` : undefined}
        autoComplete="off"
        className="h-10"
        placeholder={t("jobs.client.placeholder")}
        value={query}
        onFocus={() => setOpen(true)}
        onBlur={() => window.setTimeout(() => setOpen(false), 120)}
        onChange={(event) => {
          setQuery(event.target.value);
          setActive(0);
          setOpen(true);
        }}
        onKeyDown={(event) => {
          if (event.key === "ArrowDown") {
            event.preventDefault();
            setActive((current) => Math.min(current + 1, options.length - 1));
          } else if (event.key === "ArrowUp") {
            event.preventDefault();
            setActive((current) => Math.max(current - 1, 0));
          } else if (event.key === "Enter" && open && options.length > 0) {
            event.preventDefault();
            pick(active);
          } else if (event.key === "Escape") {
            setOpen(false);
          }
        }}
      />
      {open ? (
        <ul
          id={listId}
          role="listbox"
          className="bg-popover text-popover-foreground absolute top-full z-50 mt-1 max-h-60 w-full overflow-y-auto rounded-lg border p-1 shadow-md"
        >
          {options.length === 0 ? (
            <li className="text-muted-foreground px-2 py-2 text-sm">{clients.isFetching ? t("common.loading") : t("jobs.client.none")}</li>
          ) : (
            options.map((option, index) => (
              <li
                key={option.partyId}
                id={`${listId}-${index}`}
                role="option"
                aria-selected={index === active}
                onMouseDown={(event) => {
                  event.preventDefault();
                  pick(index);
                }}
                onMouseEnter={() => setActive(index)}
                className={cn(
                  "flex cursor-pointer items-center justify-between gap-2 rounded-md px-2 py-1.5 text-sm",
                  index === active && "bg-muted",
                )}
              >
                <span className="truncate">{option.name}</span>
                <span className="text-muted-foreground font-mono text-xs">{option.code}</span>
              </li>
            ))
          )}
        </ul>
      ) : null}
    </div>
  );
}
