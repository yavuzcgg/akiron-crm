"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Download, FileText, Trash2, Upload } from "lucide-react";
import { useRef, type ChangeEvent } from "react";
import { toast } from "sonner";
import { Button, buttonVariants } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { refreshTimelineSoon } from "@/features/timeline/timeline-api";
import { api } from "@/lib/api/client";
import { ApiError, unwrap } from "@/lib/api/errors";
import { formatDate, formatFileSize } from "@/lib/format";
import { useI18n } from "@/lib/i18n";

interface FileListProps {
  subjectType: "workspace" | "user";
  subjectId: string;
  canWrite: boolean;
}

/** Files attached to one record, with upload and removal. Downloads go straight to the API as attachments. */
export function FileList({ subjectType, subjectId, canWrite }: FileListProps) {
  const { t, tError } = useI18n();
  const queryClient = useQueryClient();
  const input = useRef<HTMLInputElement>(null);
  const filesKey = ["files", subjectType, subjectId] as const;

  const files = useQuery({
    queryKey: filesKey,
    queryFn: async () => unwrap(await api.GET("/api/v1/files", { params: { query: { subjectType, subjectId } } })),
  });

  const upload = useMutation({
    mutationFn: async (file: File) => {
      const form = new FormData();
      form.append("file", file);
      form.append("subjectType", subjectType);
      form.append("subjectId", subjectId);

      // The generated body type describes the multipart fields; FormData is passed through as is.
      return unwrap(await api.POST("/api/v1/files", { body: form as never, bodySerializer: (body) => body as unknown as FormData }));
    },
    onSuccess: (file) => {
      toast.success(t("files.uploaded", { name: file.fileName }));
      void queryClient.invalidateQueries({ queryKey: filesKey });
      refreshTimelineSoon(queryClient);
    },
    onError: (error) => toast.error(tError(error instanceof ApiError ? error.code : "common.network", error instanceof ApiError ? error.params : undefined)),
  });

  const remove = useMutation({
    mutationFn: async (id: string) => {
      unwrap(await api.DELETE("/api/v1/files/{id}", { params: { path: { id } } }));
    },
    onSuccess: () => {
      toast.success(t("files.deleted"));
      void queryClient.invalidateQueries({ queryKey: filesKey });
    },
  });

  const onPick = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (file) upload.mutate(file);
  };

  return (
    <div className="grid gap-3">
      {canWrite ? (
        <div>
          <input ref={input} type="file" className="hidden" onChange={onPick} />
          <Button variant="outline" size="sm" onClick={() => input.current?.click()} disabled={upload.isPending}>
            <Upload /> {upload.isPending ? t("files.uploading") : t("files.upload")}
          </Button>
        </div>
      ) : null}

      {files.isPending ? (
        <Skeleton className="h-10 w-full" />
      ) : !files.data?.length ? (
        <p className="text-muted-foreground text-sm">{t("files.empty")}</p>
      ) : (
        <ul className="divide-border divide-y">
          {files.data.map((file) => (
            <li key={file.id} className="flex items-center gap-3 py-2">
              <FileText className="text-muted-foreground size-4 shrink-0" />
              <div className="grid min-w-0 flex-1">
                <span className="truncate text-sm font-medium">{file.fileName}</span>
                <span className="text-muted-foreground text-xs">
                  {formatFileSize(file.sizeBytes)} · {formatDate(file.uploadedAt)}
                </span>
              </div>
              <a
                href={`/api/v1/files/${file.id}/content`}
                className={buttonVariants({ variant: "ghost", size: "icon-sm" })}
                aria-label={file.fileName}
              >
                <Download />
              </a>
              {canWrite ? (
                <Button variant="ghost" size="icon-sm" onClick={() => remove.mutate(file.id)} aria-label={t("files.delete")}>
                  <Trash2 />
                </Button>
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
