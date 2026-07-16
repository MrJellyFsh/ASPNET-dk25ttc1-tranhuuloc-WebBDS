import { getServerSession } from "next-auth";
import { redirect } from "next/navigation";
import { completeAgentOnboarding } from "../actions";
import { authOptions } from "@/lib/auth";

export default async function AgentOnboardingPage({ searchParams }: { searchParams: Promise<{ error?: string }> }) {
  const session = await getServerSession(authOptions);
  if (!session?.user) redirect("/dang-nhap?next=/tro-thanh-moi-gioi");
  const { error } = await searchParams;
  return <main className="grid min-h-screen place-items-center bg-[#f7faf9] p-4"><section className="w-full max-w-xl rounded-xl border border-[#dbe7e1] bg-white p-6 shadow-sm"><p className="text-sm font-semibold text-[#176b5c]">NhaTot cho moi gioi</p><h1 className="mt-2 text-2xl font-bold text-[#123b38]">Hoan tat ho so moi gioi</h1><p className="mt-2 text-sm leading-6 text-[#42605a]">Ho so se duoc luu de quan ly tin dang. Trang thai xac thuc se duoc admin phe duyet o buoc van hanh tiep theo.</p>{error ? <p role="alert" className="mt-4 text-sm text-red-700">Vui long kiem tra lai thong tin ho so.</p> : null}<form action={completeAgentOnboarding} className="mt-6 space-y-4"><label className="block text-sm font-medium">Cong ty / thuong hieu<input required name="agency" minLength={2} maxLength={120} className="mt-1 w-full rounded-lg border border-[#cdded6] px-3 py-2.5" /></label><label className="block text-sm font-medium">Gioi thieu ngan <span className="font-normal text-[#668078]">(khong bat buoc)</span><textarea name="bio" maxLength={1000} rows={4} className="mt-1 w-full rounded-lg border border-[#cdded6] px-3 py-2.5" /></label><button className="w-full rounded-lg bg-[#176b5c] px-4 py-3 font-semibold text-white">Luu ho so moi gioi</button></form></section></main>;
}
