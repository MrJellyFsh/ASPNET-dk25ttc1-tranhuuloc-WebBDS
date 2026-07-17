import { randomUUID } from "node:crypto";
import Link from "next/link";
import { ListingStatus, ListingTier, Role, TransactionPurpose, TransactionStatus } from "@prisma/client";
import { getServerSession } from "next-auth";
import { redirect } from "next/navigation";
import { authOptions } from "@/lib/auth";
import { formatVndFromCents } from "@/lib/payments/money";
import { prisma } from "@/lib/prisma";
import { deleteListing, submitListingForApproval } from "./actions";

const statusLabel: Record<ListingStatus, string> = {
  DRAFT: "Bản nháp",
  PENDING_APPROVAL: "Chờ duyệt",
  PUBLISHED: "Đã xuất bản",
  REJECTED: "Bị từ chối",
  EXPIRED: "Đã hết hạn",
  ARCHIVED: "Đã lưu trữ",
};

type DashboardSearchParams = Promise<{
  created?: string | string[];
  updated?: string | string[];
  deleted?: string | string[];
  archived?: string | string[];
  submitted?: string | string[];
  error?: string | string[];
}>;

type PlanOption = {
  id: string;
  name: string;
  tier: ListingTier;
  priceCents: number;
  durationDays: number;
  monthlyQuota: number | null;
};

type ReservedPlan = { planId: string | null; plan: PlanOption | null } | undefined;

function firstValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function planLabel(plan: PlanOption) {
  const price = plan.priceCents === 0 ? "Miễn phí" : formatVndFromCents(plan.priceCents);
  const quota = plan.monthlyQuota === null ? "" : ` · ${plan.monthlyQuota} tin/tháng`;
  return `${plan.name} · ${price} · hiển thị ${plan.durationDays} ngày${quota}`;
}

function feedbackMessage(searchParams: Awaited<DashboardSearchParams>) {
  if (firstValue(searchParams.created) === "1") return "Đã lưu bản nháp mới.";
  if (firstValue(searchParams.updated) === "1") return "Đã lưu thay đổi. Tin đã xuất bản sẽ trở lại bản nháp để gửi duyệt lại.";
  if (firstValue(searchParams.submitted) === "1") return "Đã giữ gói tin và gửi tin vào hàng đợi kiểm duyệt.";
  if (firstValue(searchParams.deleted) === "1") return "Đã xóa bản nháp chưa từng gửi duyệt.";
  if (firstValue(searchParams.archived) === "1") return "Đã gỡ tin khỏi hiển thị công khai và lưu trữ lịch sử kiểm duyệt.";
  if (firstValue(searchParams.error) === "pending-review") return "Không thể sửa tin đang chờ kiểm duyệt.";
  if (firstValue(searchParams.error) === "cannot-submit") return "Tin này không còn ở trạng thái có thể gửi duyệt.";
  if (firstValue(searchParams.error) === "plan-unavailable") return "Gói tin đã chọn không còn hoạt động. Hãy chọn gói khác.";
  if (firstValue(searchParams.error) === "free-quota-exhausted") return "Bạn đã dùng hết 2 tin miễn phí trong tháng này. Hãy nạp ví và chọn gói trả phí.";
  if (firstValue(searchParams.error) === "insufficient-wallet") return "Số dư ví không đủ cho gói đã chọn. Hãy nạp thêm tiền trước khi gửi duyệt.";
  if (firstValue(searchParams.error) === "plan-reserved") return "Tin này đã giữ một gói khác. Hãy gửi lại với đúng gói đã giữ.";
  if (firstValue(searchParams.error)) return "Không thể thực hiện thao tác. Vui lòng thử lại.";
  return null;
}

export default async function AgentAreaPage({ searchParams }: { searchParams: DashboardSearchParams }) {
  const [query, session] = await Promise.all([searchParams, getServerSession(authOptions)]);
  const feedback = feedbackMessage(query);
  if (!session?.user?.id) redirect("/dang-nhap");
  if (session.user.role !== Role.AGENT && session.user.role !== Role.ADMIN) redirect("/tro-thanh-moi-gioi");

  // Đọc lại role và ví từ database để JWT cũ không mở được tài khoản môi giới đã bị thu quyền.
  const broker = await prisma.user.findFirst({
    where: { id: session.user.id, role: { in: [Role.AGENT, Role.ADMIN] } },
    select: { id: true, name: true, walletCents: true },
  });
  if (!broker) redirect("/tro-thanh-moi-gioi");

  const [listings, plans] = await Promise.all([
    prisma.listing.findMany({
      where: { ownerId: broker.id },
      include: {
        propertyType: { select: { name: true } },
        images: { orderBy: { sortOrder: "asc" }, take: 1 },
        _count: { select: { auditLogs: true } },
        planTransactions: {
          where: {
            kind: TransactionPurpose.LISTING_PLAN,
            status: TransactionStatus.COMPLETED,
            appliedAt: null,
          },
          orderBy: { createdAt: "desc" },
          take: 1,
          select: {
            planId: true,
            plan: { select: { id: true, name: true, tier: true, priceCents: true, durationDays: true, monthlyQuota: true } },
          },
        },
      },
      orderBy: { updatedAt: "desc" },
    }),
    prisma.listingPlan.findMany({
      where: { isActive: true },
      select: { id: true, name: true, tier: true, priceCents: true, durationDays: true, monthlyQuota: true },
      orderBy: { createdAt: "asc" },
    }),
  ]);

  return (
    <main className="min-h-screen bg-[#f7faf9] p-4 sm:p-6">
      <section className="mx-auto max-w-5xl">
        <header className="flex flex-col justify-between gap-4 border-b border-[#dbe7e1] pb-6 sm:flex-row sm:items-end">
          <div>
            <p className="text-sm font-bold text-[#176b5c]">Khu vực môi giới</p>
            <h1 className="mt-1 text-3xl font-bold text-[#123b38]">Tin đăng của {broker.name}</h1>
            <p className="mt-2 text-[#42605a]">Tạo bản nháp, chọn gói phù hợp và gửi tin vào quy trình kiểm duyệt.</p>
          </div>
          <div className="flex flex-wrap gap-3">
            <Link href="/moi-gioi/vi" className="rounded-lg border border-[#b9d2c8] px-4 py-3 text-center text-sm font-bold text-[#176b5c]">
              Ví: {formatVndFromCents(broker.walletCents)}
            </Link>
            <Link href="/moi-gioi/tin-moi" className="rounded-lg bg-[#176b5c] px-4 py-3 text-center font-bold text-white">
              + Tạo tin mới
            </Link>
          </div>
        </header>

        {feedback ? <p className="mt-5 rounded-lg border border-[#b9d2c8] bg-[#eef6f2] px-4 py-3 text-sm font-medium text-[#254b45]" role="status">{feedback}</p> : null}

        {listings.length ? (
          <div className="mt-6 grid gap-4">
            {listings.map((listing) => {
              const canSubmit = listing.status === ListingStatus.DRAFT || listing.status === ListingStatus.REJECTED;
              const isPending = listing.status === ListingStatus.PENDING_APPROVAL;
              const reservedPlan = listing.planTransactions[0] as ReservedPlan;
              const selectedPlanId = reservedPlan?.planId ?? "";
              const hasSelectedPlanInList = plans.some((plan) => plan.id === selectedPlanId);

              return (
                <article key={listing.id} className="flex flex-col gap-4 rounded-xl border border-[#dbe7e1] bg-white p-5 shadow-sm sm:flex-row sm:items-center sm:justify-between">
                  <div>
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="rounded-full bg-[#e7f1ed] px-2.5 py-1 text-xs font-bold text-[#176b5c]">{statusLabel[listing.status]}</span>
                      <span className="text-sm text-[#668078]">{listing.propertyType.name} · {listing.intent === "SELL" ? "Bán" : "Cho thuê"}</span>
                    </div>
                    <h2 className="mt-2 font-bold text-[#123b38]">{listing.title}</h2>
                    <p className="mt-1 text-sm text-[#42605a]">{listing.district}, {listing.province} · cập nhật {listing.updatedAt.toLocaleDateString("vi-VN")}</p>
                    {reservedPlan?.plan ? <p className="mt-2 text-sm font-medium text-[#176b5c]">Đã giữ gói: {reservedPlan.plan.name}</p> : null}
                    {listing.rejectionNote ? <p className="mt-2 text-sm text-red-700">Lý do từ chối gần nhất: {listing.rejectionNote}</p> : null}
                  </div>
                  <div className="flex flex-wrap gap-3">
                    {canSubmit && (plans.length || reservedPlan?.plan) ? (
                      <form action={submitListingForApproval} className="flex flex-wrap items-end gap-2">
                        <input type="hidden" name="id" value={listing.id} />
                        {/* Giá trị ổn định của form này giúp bấm đôi chỉ phát lại giao dịch cũ, không trừ ví lần hai. */}
                        <input type="hidden" name="idempotencyKey" value={randomUUID()} />
                        <label className="grid gap-1 text-xs font-semibold text-[#42605a]">
                          Gói tin
                          <select name="planId" required defaultValue={selectedPlanId} className="rounded-lg border border-[#b9d2c8] bg-white px-3 py-2 text-sm font-medium text-[#123b38]">
                            <option value="" disabled>Chọn gói</option>
                            {reservedPlan?.plan && !hasSelectedPlanInList ? <option value={reservedPlan.plan.id}>{planLabel(reservedPlan.plan)} (đã giữ)</option> : null}
                            {plans.map((plan) => <option key={plan.id} value={plan.id}>{planLabel(plan)}</option>)}
                          </select>
                        </label>
                        <button className="rounded-lg bg-[#176b5c] px-4 py-2 text-sm font-bold text-white">Gửi duyệt</button>
                      </form>
                    ) : null}
                    {canSubmit && !plans.length && !reservedPlan?.plan ? <span className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-2 text-sm font-bold text-amber-800">Chưa có gói tin khả dụng</span> : null}
                    {isPending ? <span className="rounded-lg border border-[#b9d2c8] px-4 py-2 text-sm font-bold text-[#668078]">Đang được kiểm duyệt</span> : <Link href={`/moi-gioi/tin/${listing.id}`} className="rounded-lg border border-[#b9d2c8] px-4 py-2 text-sm font-bold text-[#176b5c]">Chỉnh sửa</Link>}
                    <form action={deleteListing}>
                      <input type="hidden" name="id" value={listing.id} />
                      <button className="rounded-lg border border-red-200 px-4 py-2 text-sm font-bold text-red-700">{listing._count.auditLogs ? "Gỡ tin" : "Xóa"}</button>
                    </form>
                  </div>
                </article>
              );
            })}
          </div>
        ) : (
          <div className="mt-6 rounded-xl border border-dashed border-[#b9d2c8] bg-white p-10 text-center">
            <h2 className="text-xl font-bold text-[#123b38]">Bạn chưa có tin đăng</h2>
            <p className="mt-2 text-[#42605a]">Bắt đầu bằng một bản nháp, chọn gói tin, sau đó gửi vào hàng đợi kiểm duyệt.</p>
            <Link href="/moi-gioi/tin-moi" className="mt-5 inline-block font-bold text-[#176b5c]">Tạo tin đầu tiên →</Link>
          </div>
        )}
      </section>
    </main>
  );
}
