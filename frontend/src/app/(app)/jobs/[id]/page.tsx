"use client";

import { use } from "react";
import { WorkOrderDetail } from "@/features/jobs/work-order-detail";

export default function WorkOrderPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  return <WorkOrderDetail id={id} />;
}
