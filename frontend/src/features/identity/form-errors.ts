import type { FieldValues, Path, UseFormSetError } from "react-hook-form";
import { ApiError } from "@/lib/api/errors";
import type { TranslationParams } from "@/lib/i18n/translate";

type TranslateError = (code: string | undefined, params?: TranslationParams) => string;

/**
 * Puts the API's field errors onto the matching form fields and returns the message for the form
 * as a whole (or null when every error belonged to a field).
 */
export function applyApiError<T extends FieldValues>(
  error: unknown,
  fields: readonly Path<T>[],
  setError: UseFormSetError<T>,
  tError: TranslateError,
): string | null {
  if (!(error instanceof ApiError)) return tError("common.network");

  let unmatched = false;
  for (const [field, failures] of Object.entries(error.fieldErrors)) {
    const first = failures[0];
    if (first && (fields as readonly string[]).includes(field)) {
      setError(field as Path<T>, { type: "server", message: tError(first.code, first.params) });
    } else {
      unmatched = true;
    }
  }

  const hasFieldErrors = Object.keys(error.fieldErrors).length > 0;
  return hasFieldErrors && !unmatched ? null : tError(error.code, error.params);
}
