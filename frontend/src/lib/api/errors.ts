import type { TranslationParams } from "@/lib/i18n/translate";

/** One rejected rule on one field, as the API reports it (ADR-0006). */
export interface FieldError {
  code: string;
  message: string;
  params?: TranslationParams;
}

interface ProblemBody {
  code?: string;
  detail?: string | null;
  params?: TranslationParams;
  errors?: Record<string, FieldError[]>;
  correlationId?: string;
}

/**
 * A failed API call. The UI never shows `detail` (English, for logs); it translates `code`, and
 * form fields translate their own `fieldErrors`.
 */
export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly params: TranslationParams;
  readonly fieldErrors: Record<string, FieldError[]>;

  constructor(status: number, body: ProblemBody | undefined) {
    super(body?.detail ?? `API request failed with status ${status}`);
    this.name = "ApiError";
    this.status = status;
    this.code = body?.code ?? (status === 0 ? "common.network" : "common.unexpected");
    this.params = { ...body?.params, correlationId: body?.correlationId ?? "-" };
    this.fieldErrors = body?.errors ?? {};
  }
}

/**
 * Turns an openapi-fetch result into data or a thrown ApiError, so TanStack Query sees failures
 * as errors.
 */
export function unwrap<T>(result: { data?: T; error?: unknown; response: Response }): T {
  if (result.response.ok) return result.data as T;
  throw new ApiError(result.response.status, result.error as ProblemBody | undefined);
}
