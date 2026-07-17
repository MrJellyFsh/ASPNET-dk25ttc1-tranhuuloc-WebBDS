import { randomUUID } from "node:crypto";
import Link from "next/link";
import { PaymentMethod, Role, TransactionPurpose, TransactionStatus, WalletEntryKind } from "@prisma/client";
import { getServerSession } from "next-auth";
import { redirect } from "next/navigation";
import { authOptions } from "@/lib/auth";
import { formatVndFromCents } from "@/lib/payments/money";
import { prisma } from "@/lib/prisma";
import { createDemoWebhookPayment, topUpDemo } from "./actions";

type WalletSearchParams = Promise<{
  credited?: string | string[];
  replayed?: string | string[];
  webhook?: string | string[];
  reference?: string | string[];
  error?: string | string[];
}>;

function firstValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function purposeLabel(purpose: TransactionPurpose) {
  return purpose === TransactionPurpose.WALLET_TOP_UP ? "Nạp ví" : "Dùng gói tin";
}

function statusLabel(status: TransactionStatus) {
  if (status === TransactionStatus.COMPLETED) return "Hoàn tất";
  if (status === TransactionStatus.PENDING) return "Chờ thanh toán";
  if (status === TransactionStatus.FAILED) return "Thất bại";
  return "Đã hoàn tiền";
}

function methodLabel(method: PaymentMethod) {
  if (method === PaymentMethod.WALLET) return "Ví NhaTot";
  if (method === PaymentMethod.DEMO) return "Mô phỏng";
  return method === PaymentMethod.VNPAY ? "VNPay" : "MoMo";
}

function planSummary(plan: { name: string; priceCents: number; durationDays: number; monthlyQuota: number | null }) {
  const quota = plan.monthlyQuota === null ? "" : ` · ${plan.monthlyQuota} tin/tháng`;
  return `${plan.priceCents === 0 ? "Miễn phí" : formatVndFromCents(plan.priceCents)} · ${plan.durationDays} ngày${quota}`;
}

function feedback(searchParams: Awaited<WalletSearchParams>) {
  if (firstValue(searchParams.credited) === "1") return "Đã nạp tiền mô phỏng vào ví và ghi sổ giao dịch.";
  if (firstValue(searchParams.replayed) === "1") return "Yêu cầu nạp này đã được xử lý trước đó; số dư không bị cộng hai lần.";
  if (firstValue(searchParams.webhook) === "pending") return `Đã tạo giao dịch chờ webhook${firstValue(searchParams.reference) ? `: ${firstValue(searchParams.reference)}` : ""}.`;
  if (firstValue(searchParams.error) === "demo-disabled") return "Tính năng nạp mô phỏng chỉ có ở môi trường development.";
  if (firstValue(searchParams.error) === "wallet-limit") return "Số dư sẽ vượt ngưỡng an toàn của ví; yêu cầu nạp chưa được thực hiện.";
  if (firstValue(searchParams.error)) return "Dữ liệu nạp tiền không hợp lệ. Vui lòng thử lại.";
  return null;
}

export default async function BrokerWalletPage({ searchParams }: { searchParams: WalletSearchParams }) {
  const [query, session] = await Promise.all([searchParams, getServerSession(authOptions)]);
  if (!session?.user?.id) redirect("/dang-nhap");
  if (session.user.role !== Role.AGENT && session.user.role !== Role.ADMIN) redirect("/tro-thanh-moi-gioi");

  const broker = await prisma.user.findFirst({
    where: { id: session.user.id, role: { in: [Role.AGENT, Role.ADMIN] } },
    select: { id: true, name: true, walletCents: true },
  });
  if (!broker) redirect("/tro-thanh-moi-gioi");

  const [plans, walletEntries, transactions] = await Promise.all([
    prisma.listingPlan.findMany({
      where: { isActive: true },
      select: { id: true, name: true, priceCents: true, durationDays: true, monthlyQuota: true },
      orderBy: { createdAt: "asc" },
    }),
    prisma.walletEntry.findMany({
      where: { userId: broker.id },
      include: { transaction: { select: { method: true, plan: { select: { name: true } }, listing: { select: { title: true } } } } },
      orderBy: { createdAt: "desc" },
      take: 12,
    }),
    prisma.transaction.findMany({
      where: { userId: broker.id },
      include: { plan: { select: { name: true } }, listing: { select: { title: true } } },
      orderBy: { createdAt: "desc" },
      take: 12,
    }),
  ]);

  const message = feedback(query);
  const showDemoTools = process.env.NODE_ENV !== "production";

  return (
    <main className="min-h-screen bg-[#f7faf9] p-4 sm:p-6">
      <section className="mx-auto max-w-5xl">
        <header className="flex flex-col justify-between gap-4 border-b border-[#dbe7e1] pb-6 sm:flex-row sm:items-end">
          <div>
            <Link href="/moi-gioi" className="text-sm font-bold text-[#176b5c]">← Quay lại dashboard</Link>
            <p className="mt-4 text-sm font-bold text-[#176b5c]">Ví & gói tin</p>
            <h1 className="mt-1 text-3xl font-bold text-[#123b38]">Ví của {broker.name}</h1>
            <p className="mt-2 max-w-2xl text-[#42605a]">Nạp tiền để dùng gói Standard/VIP. Gói miễn phí được kiểm tra quota khi bạn gửi tin duyệt.</p>
          </div>
          <div className="rounded-xl bg-[#123b38] px-5 py-4 text-white shadow-sm">
            <p className="text-sm font-medium text-[#c6ddd5]">Số dư khả dụng</p>
            <p className="mt-1 text-2xl font-bold">{formatVndFromCents(broker.walletCents)}</p>
          </div>
        </header>

        {message ? <p className="mt-5 rounded-lg border border-[#b9d2c8] bg-[#eef6f2] px-4 py-3 text-sm font-medium text-[#254b45]" role="status">{message}</p> : null}

        <section className="mt-6">
          <h2 className="text-xl font-bold text-[#123b38]">Gói tin đang khả dụng</h2>
          <div className="mt-3 grid gap-4 md:grid-cols-3">
            {plans.map((plan) => (
              <article key={plan.id} className="rounded-xl border border-[#dbe7e1] bg-white p-5 shadow-sm">
                <h3 className="font-bold text-[#123b38]">{plan.name}</h3>
                <p className="mt-2 text-lg font-bold text-[#176b5c]">{plan.priceCents === 0 ? "Miễn phí" : formatVndFromCents(plan.priceCents)}</p>
                <p className="mt-2 text-sm text-[#42605a]">{planSummary(plan)}</p>
                <p className="mt-3 text-xs leading-5 text-[#668078]">Gói được giữ khi gửi duyệt. Tiền chỉ bị trừ một lần, kể cả khi tin bị từ chối và gửi lại.</p>
              </article>
            ))}
          </div>
        </section>

        {showDemoTools ? (
          <section className="mt-7 grid gap-4 lg:grid-cols-2">
            <form action={topUpDemo} className="rounded-xl border border-[#b9d2c8] bg-white p-5 shadow-sm">
              <h2 className="text-xl font-bold text-[#123b38]">Nạp ví mô phỏng</h2>
              <p className="mt-2 text-sm text-[#42605a]">Dùng để kiểm tra trải nghiệm mua gói trong môi trường phát triển.</p>
              <input type="hidden" name="idempotencyKey" value={randomUUID()} />
              <label className="mt-4 block text-sm font-semibold text-[#254b45]">
                Số tiền nạp
                <select name="amountVnd" defaultValue="1000000" className="mt-1 w-full rounded-lg border border-[#cdded6] bg-white px-3 py-2.5 font-normal outline-none focus:border-[#176b5c]">
                  <option value="500000">500.000 đ</option>
                  <option value="1000000">1.000.000 đ</option>
                  <option value="3000000">3.000.000 đ</option>
                  <option value="5000000">5.000.000 đ</option>
                </select>
              </label>
              <button className="mt-4 rounded-lg bg-[#176b5c] px-5 py-3 text-sm font-bold text-white">Nạp thử ngay</button>
            </form>

            <form action={createDemoWebhookPayment} className="rounded-xl border border-dashed border-[#b9d2c8] bg-[#eef6f2] p-5">
              <h2 className="text-xl font-bold text-[#123b38]">Kiểm tra webhook mô phỏng</h2>
              <p className="mt-2 text-sm text-[#42605a]">Tạo giao dịch chờ rồi gửi callback HMAC đến endpoint demo theo hướng dẫn README.</p>
              <input type="hidden" name="idempotencyKey" value={randomUUID()} />
              <label className="mt-4 block text-sm font-semibold text-[#254b45]">
                Số tiền callback
                <select name="amountVnd" defaultValue="1000000" className="mt-1 w-full rounded-lg border border-[#cdded6] bg-white px-3 py-2.5 font-normal outline-none focus:border-[#176b5c]">
                  <option value="500000">500.000 đ</option>
                  <option value="1000000">1.000.000 đ</option>
                  <option value="3000000">3.000.000 đ</option>
                </select>
              </label>
              <button className="mt-4 rounded-lg border border-[#176b5c] bg-white px-5 py-3 text-sm font-bold text-[#176b5c]">Tạo giao dịch chờ callback</button>
            </form>
          </section>
        ) : (
          <p className="mt-7 rounded-xl border border-[#dbe7e1] bg-white p-5 text-sm text-[#42605a]">Cổng thanh toán thật sẽ khởi tạo giao dịch chờ và nạp ví sau callback đã xác minh. Công cụ mô phỏng đã được tắt trên production.</p>
        )}

        {/* Sổ cái tách khỏi số dư thay đổi được để mỗi khoản cộng/trừ đều có thể giải thích và đối soát. */}
        <section className="mt-7 grid gap-6 lg:grid-cols-2">
          <div>
            <h2 className="text-xl font-bold text-[#123b38]">Sổ ví gần đây</h2>
            <div className="mt-3 overflow-hidden rounded-xl border border-[#dbe7e1] bg-white shadow-sm">
              {walletEntries.length ? walletEntries.map((entry) => (
                <div key={entry.id} className="flex items-center justify-between gap-4 border-b border-[#edf2ef] p-4 last:border-b-0">
                  <div>
                    <p className="font-semibold text-[#123b38]">{entry.kind === WalletEntryKind.TOP_UP ? "Nạp tiền" : entry.transaction.plan?.name ?? "Dùng gói tin"}</p>
                    <p className="mt-1 text-xs text-[#668078]">{entry.transaction.listing?.title ?? methodLabel(entry.transaction.method)} · {entry.createdAt.toLocaleString("vi-VN")}</p>
                  </div>
                  <div className="text-right">
                    <p className={`font-bold ${entry.amountCents >= 0 ? "text-[#176b5c]" : "text-red-700"}`}>{entry.amountCents >= 0 ? "+" : ""}{formatVndFromCents(entry.amountCents)}</p>
                    <p className="mt-1 text-xs text-[#668078]">Số dư: {formatVndFromCents(entry.balanceAfterCents)}</p>
                  </div>
                </div>
              )) : <p className="p-5 text-sm text-[#668078]">Chưa có biến động số dư.</p>}
            </div>
          </div>

          <div>
            <h2 className="text-xl font-bold text-[#123b38]">Giao dịch gần đây</h2>
            <div className="mt-3 overflow-hidden rounded-xl border border-[#dbe7e1] bg-white shadow-sm">
              {transactions.length ? transactions.map((transaction) => (
                <div key={transaction.id} className="flex items-center justify-between gap-4 border-b border-[#edf2ef] p-4 last:border-b-0">
                  <div>
                    <p className="font-semibold text-[#123b38]">{purposeLabel(transaction.kind)}{transaction.plan ? ` · ${transaction.plan.name}` : ""}</p>
                    <p className="mt-1 text-xs text-[#668078]">{methodLabel(transaction.method)} · {transaction.listing?.title ?? transaction.providerReference ?? "Ví NhaTot"}</p>
                  </div>
                  <div className="text-right">
                    <p className="font-bold text-[#123b38]">{formatVndFromCents(transaction.amountCents)}</p>
                    <p className="mt-1 text-xs text-[#668078]">{statusLabel(transaction.status)}</p>
                  </div>
                </div>
              )) : <p className="p-5 text-sm text-[#668078]">Chưa có giao dịch.</p>}
            </div>
          </div>
        </section>
      </section>
    </main>
  );
}
