import Link from "next/link";
import { LoginForm } from "../login-form";

export default async function LoginPage({ searchParams }: { searchParams: Promise<{ created?: string; agent?: string; error?: string }> }) {
  const params = await searchParams;
  const notice = params.agent ? "Ho so moi gioi da duoc tao. Hay dang nhap lai de cap nhat quyen." : params.created ? "Tao tai khoan thanh cong. Hay dang nhap de tiep tuc." : params.error === "email-exists" ? "Email nay da duoc dang ky. Hay dang nhap." : null;
  return <main className="grid min-h-screen place-items-center bg-[#f7faf9] p-4"><section className="w-full max-w-md rounded-xl border border-[#dbe7e1] bg-white p-6 shadow-sm"><Link href="/" className="text-lg font-bold text-[#176b5c]">NhaTot</Link><h1 className="mt-5 text-2xl font-bold text-[#123b38]">Dang nhap</h1><p className="mt-1 text-sm text-[#42605a]">Quan ly trai nghiem tim nha va tin dang cua ban.</p>{notice ? <p className="mt-4 rounded-lg bg-[#e7f1ed] p-3 text-sm text-[#176b5c]">{notice}</p> : null}<LoginForm /><p className="mt-5 text-center text-sm text-[#42605a]">Chua co tai khoan? <Link className="font-semibold text-[#176b5c]" href="/dang-ky">Dang ky</Link></p></section></main>;
}
