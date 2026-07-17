import Link from "next/link";
import { getServerSession } from "next-auth";
import { Role } from "@prisma/client";
import { redirect } from "next/navigation";
import { authOptions } from "@/lib/auth";
import { prisma } from "@/lib/prisma";
import { createListing } from "../actions";
import { ListingForm } from "../listing-form";

export default async function NewListingPage() {
  const session = await getServerSession(authOptions);
  if (!session?.user) redirect("/dang-nhap");
  if (session.user.role !== Role.AGENT && session.user.role !== Role.ADMIN) redirect("/tro-thanh-moi-gioi");
  const propertyTypes = await prisma.propertyType.findMany({ orderBy: { name: "asc" } });
  return <main className="min-h-screen bg-[#f7faf9] p-4 sm:p-6"><section className="mx-auto max-w-3xl rounded-xl border border-[#dbe7e1] bg-white p-5 shadow-sm sm:p-8"><Link href="/moi-gioi" className="text-sm font-bold text-[#176b5c]">← Quay lại dashboard</Link><h1 className="mt-4 text-3xl font-bold text-[#123b38]">Tạo tin mới</h1><p className="mt-2 text-[#42605a]">Tin được lưu ở trạng thái bản nháp để bạn kiểm tra trước.</p><div className="mt-7"><ListingForm propertyTypes={propertyTypes} action={createListing} submitLabel="Lưu bản nháp" /></div></section></main>;
}
