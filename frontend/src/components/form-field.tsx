"use client";

import { Eye, EyeOff } from "lucide-react";
import { useState, type ComponentProps } from "react";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";

interface FormFieldProps extends ComponentProps<typeof Input> {
  id: string;
  label: string;
  error?: string;
  hint?: string;
}

/**
 * Label, input and the field's error or hint, wired for screen readers (design system: visible
 * label, error under the field via aria-describedby, show/hide toggle on passwords).
 */
export function FormField({ id, label, error, hint, type, className, ...inputProps }: FormFieldProps) {
  const { t } = useI18n();
  const [revealed, setRevealed] = useState(false);
  const isPassword = type === "password";
  const describedBy = error ? `${id}-error` : hint ? `${id}-hint` : undefined;

  return (
    <div className="grid gap-1.5">
      <Label htmlFor={id} className="text-[13px] font-medium">
        {label}
      </Label>
      <div className="relative">
        <Input
          id={id}
          type={isPassword && revealed ? "text" : type}
          aria-invalid={!!error}
          aria-describedby={describedBy}
          className={cn("h-10", isPassword && "pr-10", className)}
          {...inputProps}
        />
        {isPassword ? (
          <button
            type="button"
            onClick={() => setRevealed((value) => !value)}
            className="text-muted-foreground hover:text-foreground focus-visible:text-foreground absolute inset-y-0 right-0 flex w-10 items-center justify-center rounded-r-lg transition-colors"
            aria-label={t(revealed ? "auth.password.hide" : "auth.password.show")}
            aria-pressed={revealed}
          >
            {revealed ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
          </button>
        ) : null}
      </div>
      {error ? (
        <p id={`${id}-error`} role="alert" className="text-destructive text-[13px]">
          {error}
        </p>
      ) : hint ? (
        <p id={`${id}-hint`} className="text-muted-foreground text-[13px]">
          {hint}
        </p>
      ) : null}
    </div>
  );
}
