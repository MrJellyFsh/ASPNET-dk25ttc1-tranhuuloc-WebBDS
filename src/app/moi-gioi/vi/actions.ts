"use server";

import { PaymentMethod, Role } from "@prisma/client";
import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { getServerSession } from "next-auth";
import { z } from "zod";
import { authOptions } from "@/lib/auth";
import { MAX_TOP_UP_VND, MIN_TOP_UP_VND, vndToCents } from "@/lib/payments/money";
import { createPendingProviderTopUp, topUpDemoWallet } from "@/lib/payments/transactions";
import { prisma } from "@/lib/prisma";

const topUpSchema = z.object({
  amountVnd: z.coerce.number().int().min(MIN_TOP_UP_VND).max(MAX_TOP_UP_VND),
  idempotencyKey: z.uuid(),
});

function text(formData: FormData, name: string) {
  return String(formData.get(name) ?? "").trim();
}

async function requireBrokerWallet() {
  const session = await getServerSession(authOptions);
  if (!session?.user?.id) redirect("/dang-nhap");
  const broker = await prisma.user.findFirst({
    where: { id: session.user.id, role: { in: [Role.AGENT, Role.ADMIN] } },
    select: { id: true },
  });
  if (!broker) redirect("/tro-thanh-moi-gioi");
  return broker;
}

/** Nạp tiền chỉ ở development để minh họa đúng giao dịch idempotent như cổng thật. */
export async function topUpDemo(formData: FormData) {
  const broker = await requireBrokerWallet();
  if (process.env.NODE_ENV === "production") redirect("/moi-gioi/vi?error=demo-disabled");
  const parsed = topUpSchema.safeParse({ amountVnd: text(formData, "amountVnd"), idempotencyKey: text(formData, "idempotencyKey") });
  if (!parsed.success) redirect("/moi-gioi/vi?error=invalid-top-up");
  const amountCents = vndToCents(parsed.data.amountVnd);
  if (amountCents === null) redirect("/moi-gioi/vi?error=invalid-top-up");

  const outcome = await topUpDemoWallet({ userId: broker.id, amountCents, idempotencyKey: parsed.data.idempotencyKey });
  if (outcome.code === "wallet-limit") redirect("/moi-gioi/vi?error=wallet-limit");
  if (outcome.code === "idempotency-conflict") redirect("/moi-gioi/vi?error=invalid-top-up");

  revalidatePath("/moi-gioi");
  revalidatePath("/moi-gioi/vi");
  redirect(`/moi-gioi/vi?${outcome.code === "replayed" ? "replayed" : "credited"}=1`);
}

/**
 * Tạo đơn demo PENDING để integration test POST callback có chữ ký vào
 * /api/payments/webhook/demo và đi qua đúng đường settlement như production.
 */
export async function createDemoWebhookPayment(formData: FormData) {
  const broker = await requireBrokerWallet();
  if (process.env.NODE_ENV === "production") redirect("/moi-gioi/vi?error=demo-disabled");
  const parsed = topUpSchema.safeParse({ amountVnd: text(formData, "amountVnd"), idempotencyKey: text(formData, "idempotencyKey") });
  if (!parsed.success) redirect("/moi-gioi/vi?error=invalid-top-up");
  const amountCents = vndToCents(parsed.data.amountVnd);
  if (amountCents === null) redirect("/moi-gioi/vi?error=invalid-top-up");

  const outcome = await createPendingProviderTopUp({
    userId: broker.id,
    amountCents,
    idempotencyKey: parsed.data.idempotencyKey,
    method: PaymentMethod.DEMO,
  });
  if (outcome.code === "idempotency-conflict" || !outcome.providerReference) redirect("/moi-gioi/vi?error=invalid-top-up");

  revalidatePath("/moi-gioi/vi");
  redirect(`/moi-gioi/vi?webhook=pending&reference=${encodeURIComponent(outcome.providerReference)}`);
}
