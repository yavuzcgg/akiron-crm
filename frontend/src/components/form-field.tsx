import type { ComponentProps } from "react";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

interface FormFieldProps extends ComponentProps<typeof Input> {
  id: string;
  label: string;
  error?: string;
  hint?: string;
}

/** Label, input and the field's error or hint, wired for screen readers. */
export function FormField({ id, label, error, hint, ...inputProps }: FormFieldProps) {
  const describedBy = error ? `${id}-error` : hint ? `${id}-hint` : undefined;

  return (
    <div className="grid gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Input id={id} aria-invalid={!!error} aria-describedby={describedBy} {...inputProps} />
      {error ? (
        <p id={`${id}-error`} className="text-destructive text-sm">
          {error}
        </p>
      ) : hint ? (
        <p id={`${id}-hint`} className="text-muted-foreground text-sm">
          {hint}
        </p>
      ) : null}
    </div>
  );
}
