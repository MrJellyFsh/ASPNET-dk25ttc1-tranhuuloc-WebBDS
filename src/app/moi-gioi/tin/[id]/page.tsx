import Link from "next/link";
import { ListingStatus, Role } from "@prisma/client";
import { getServerSession } from "next-auth";
import { notFound, redirect } from "next/navigation";
import { authOptions } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { updateListing } from "../../actions";
import { ListingForm } from "../../listing-form";

export default async function EditListingPage({ params }: { params: Promise<{ id: string }> }) {
  const session = await getServerSession(authOptions);
  if (!session?.user) redirect("/dang-nhap");
  if (session.user.role !== Role.AGENT && session.user.role !== Role.ADMIN) redirect("/tro-thanh-moi-gioi");
  const { id } = await params;
  const [listing, propertyTypes] = await Promise.all([prisma.listing.findFirst({ where: { id, ownerId: session.user.id }, include: { images: { orderBy: { sortOrder: "asc" } } } }), prisma.propertyType.findMany({ orderBy: { name: "asc" } })]);
  if (!listing) notFound();
  if (listing.status === ListingStatus.PENDING_APPROVAL) redirect("/moi-gioi?error=pending-review");
  return <main className="min-h-screen bg-[#f7faf9] p-4 sm:p-6"><section className="mx-auto max-w-3xl rounded-xl border border-[#dbe7e1] bg-white p-5 shadow-sm sm:p-8"><Link href="/moi-gioi" className="text-sm font-bold text-[#176b5c]">← Quay lại dashboard</Link><h1 className="mt-4 text-3xl font-bold text-[#123b38]">Chỉnh sửa tin</h1><p className="mt-2 text-[#42605a]">Mọi thay đổi vẫn do bạn sở hữu và sẽ được kiểm tra lại khi gửi duyệt.</p><div className="mt-7"><ListingForm propertyTypes={propertyTypes} listing={listing} action={updateListing.bind(null, listing.id)} submitLabel="Lưu thay đổi" /></div></section></main>;
}
