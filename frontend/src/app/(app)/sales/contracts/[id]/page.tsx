"use client";

import { use } from "react";
import { ContractDetail } from "@/features/contracts/contract-detail";

export default function ContractRoute({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  return <ContractDetail id={id} />;
}
